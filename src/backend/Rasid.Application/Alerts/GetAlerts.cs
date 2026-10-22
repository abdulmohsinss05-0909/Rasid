using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Application.Connectors;
using Rasid.Application.Monitoring;
using Rasid.Domain.Common;
using Rasid.Domain.Engagement;
using Rasid.Domain.Insights;
using Rasid.Domain.Quota;
using Rasid.Domain.Topics;

namespace Rasid.Application.Alerts;

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
}

public enum AlertKind
{
    ActivitySpike = 1,
    CollectionProblem = 2,
    StaleData = 3,
    Quota = 4,
}

/// <param name="Id">Stable identifier, so a client can remember which alerts were already seen.</param>
public sealed record AlertDto(
    string Id,
    AlertKind Kind,
    AlertSeverity Severity,
    string Title,
    string Message,
    Guid? TopicId,
    string? TopicName,
    DateTimeOffset OccurredUtc);

public sealed record GetAlertsQuery : IQuery<IReadOnlyList<AlertDto>>;

/// <summary>
/// Alerts derived on demand from current state: activity spikes in the last 24 hours, collection
/// problems, stale data and API budget pressure. Nothing is stored; the list always reflects reality.
/// </summary>
internal sealed class GetAlertsHandler(
    IRasidDbContext db,
    IPostSourceResolver sourceResolver,
    IQuotaManager quota,
    IOptions<MonitoringOptions> options,
    TimeProvider clock) : IRequestHandler<GetAlertsQuery, IReadOnlyList<AlertDto>>
{
    private static readonly TimeSpan SpikeBaseline = TimeSpan.FromDays(7);
    private static readonly TimeSpan SpikeWindow = TimeSpan.FromHours(24);

    public async Task<Result<IReadOnlyList<AlertDto>>> HandleAsync(GetAlertsQuery request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var settings = options.Value;
        var alerts = new List<AlertDto>();

        var topics = await db.Topics.AsNoTracking().Where(t => t.IsActive).ToListAsync(cancellationToken);
        var baselineFrom = TopicMetricsHourly.TruncateToHour(now - SpikeBaseline);
        var hourly = (await db.TopicMetricsHourly.AsNoTracking()
                .Where(h => h.HourUtc >= baselineFrom)
                .Select(h => new { h.TopicId, h.HourUtc, h.Posts })
                .ToListAsync(cancellationToken))
            .ToLookup(h => h.TopicId);

        foreach (var topic in topics)
        {
            // Activity spike: an hour in the last 24 h far above the topic's usual hourly level.
            var byHour = hourly[topic.Id].ToDictionary(h => h.HourUtc, h => h.Posts);
            var hours = Enumerable.Range(0, (int)SpikeBaseline.TotalHours).Select(i => baselineFrom.AddHours(i)).ToList();
            var spikes = InsightMath.DetectSpikes([.. hours.Select(h => byHour.GetValueOrDefault(h))], minimum: 5);
            var lastSpike = hours.Select((h, i) => (Hour: h, IsSpike: spikes[i]))
                .LastOrDefault(x => x.IsSpike && x.Hour >= now - SpikeWindow);
            if (lastSpike.IsSpike)
            {
                alerts.Add(new AlertDto(
                    $"spike:{topic.Id:N}:{lastSpike.Hour.UtcTicks.ToString(CultureInfo.InvariantCulture)}",
                    AlertKind.ActivitySpike,
                    AlertSeverity.Info,
                    $"Activity spike in \"{topic.Name}\"",
                    $"{byHour[lastSpike.Hour]} posts in one hour, well above the usual level.",
                    topic.Id,
                    topic.Name,
                    lastSpike.Hour));
            }

            if (topic.LastRefreshStatus is RefreshStatus.AuthFailed or RefreshStatus.ConnectionFailed
                or RefreshStatus.RateLimited or RefreshStatus.NotConfigured or RefreshStatus.Deferred)
            {
                alerts.Add(new AlertDto(
                    $"collection:{topic.Id:N}:{topic.LastRefreshStatus}:{(topic.LastRefreshAttemptUtc ?? now).UtcTicks.ToString(CultureInfo.InvariantCulture)}",
                    AlertKind.CollectionProblem,
                    topic.LastRefreshStatus is RefreshStatus.AuthFailed or RefreshStatus.NotConfigured ? AlertSeverity.Critical : AlertSeverity.Warning,
                    $"Collection issue in \"{topic.Name}\"",
                    CollectionMessage(topic.LastRefreshStatus),
                    topic.Id,
                    topic.Name,
                    topic.LastRefreshAttemptUtc ?? now));
            }
            else if (topic.IsStale(now, settings.RefreshInterval(topic.Priority)))
            {
                alerts.Add(new AlertDto(
                    $"stale:{topic.Id:N}:{(topic.LastSuccessfulRefreshUtc ?? now).UtcTicks.ToString(CultureInfo.InvariantCulture)}",
                    AlertKind.StaleData,
                    AlertSeverity.Warning,
                    $"\"{topic.Name}\" data is out of date",
                    "The latest scheduled updates have not completed. The last collected data is still shown.",
                    topic.Id,
                    topic.Name,
                    topic.LastSuccessfulRefreshUtc ?? now));
            }
        }

        if (sourceResolver.Resolve()?.Source == DataSource.XApi)
        {
            var decision = await quota.EvaluateAsync(DataSource.XApi, TopicPriority.Normal, cancellationToken);
            if (decision.Mode is not QuotaMode.Normal)
            {
                alerts.Add(new AlertDto(
                    $"quota:{ApiUsagePeriod.PeriodKey(now)}:{decision.Mode}",
                    AlertKind.Quota,
                    decision.Mode is QuotaMode.Throttled ? AlertSeverity.Warning : AlertSeverity.Critical,
                    "X API budget",
                    $"{decision.Reason} {decision.BudgetUsedPercent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "?"}% of the monthly budget used.",
                    null,
                    null,
                    now));
            }
        }

        return alerts.OrderByDescending(a => a.Severity).ThenByDescending(a => a.OccurredUtc).ToList();
    }

    private static string CollectionMessage(RefreshStatus status) => status switch
    {
        RefreshStatus.AuthFailed => "X rejected the credentials. An administrator must update the API token.",
        RefreshStatus.RateLimited => "X API rate limit reached. Collection resumes automatically after the reset.",
        RefreshStatus.ConnectionFailed => "The data source could not be reached. Collection retries automatically.",
        RefreshStatus.NotConfigured => "No data source is configured for this environment.",
        RefreshStatus.Deferred => "Collection was postponed to protect the X API budget.",
        _ => "Collection needs attention.",
    };
}
