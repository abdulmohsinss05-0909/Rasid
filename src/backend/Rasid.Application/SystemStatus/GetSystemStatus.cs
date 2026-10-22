using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Messaging;
using Rasid.Application.Common.Options;
using Rasid.Application.Common.Results;
using Rasid.Application.Connectors;
using Rasid.Application.Dashboard;
using Rasid.Application.Monitoring;
using Rasid.Domain.Common;
using Rasid.Domain.Quota;
using Rasid.Domain.Refresh;
using Rasid.Domain.Topics;

namespace Rasid.Application.SystemStatus;

public sealed record GetSystemStatusQuery : IQuery<SystemStatusDto>;

public sealed record SystemStatusDto(
    DataSource? ActiveSource,
    string SourceLabel,
    bool SampleDataAllowed,
    QuotaStatusDto Quota,
    IReadOnlyList<RefreshRunDto> RecentRuns,
    DateTimeOffset GeneratedUtc);

public sealed record QuotaStatusDto(
    string Period,
    long PostsRead,
    long Requests,
    long MonthlyBudget,
    decimal? BudgetUsedPercent,
    QuotaMode Mode,
    string Reason,
    int? RateLimitRemaining,
    DateTimeOffset? RateLimitResetUtc);

public sealed record RefreshRunDto(
    long Id,
    Guid? TopicId,
    string? TopicName,
    RefreshJobType JobType,
    DataSource? Source,
    RefreshStatus Status,
    DateTimeOffset StartedUtc,
    DateTimeOffset? FinishedUtc,
    int PostsProcessed,
    int ApiCalls,
    string? Message);

internal sealed class GetSystemStatusHandler(
    IRasidDbContext db,
    IPostSourceResolver sourceResolver,
    IQuotaManager quota,
    IOptions<MonitoringOptions> options,
    TimeProvider clock) : IRequestHandler<GetSystemStatusQuery, SystemStatusDto>
{
    private const int RecentRunCount = 25;

    public async Task<Result<SystemStatusDto>> HandleAsync(GetSystemStatusQuery request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var source = sourceResolver.Resolve()?.Source;
        var settings = options.Value.Quota.ToSettings();
        var usage = await quota.GetCurrentPeriodAsync(cancellationToken);
        var decision = source == DataSource.Sample
            ? new QuotaDecision(QuotaMode.Normal, settings.MaxPagesPerRun, 1, null, "Sample data does not consume X API quota.")
            : QuotaPolicy.Evaluate(usage, settings, TopicPriority.Normal, now);

        var runs = await db.RefreshLogs
            .AsNoTracking()
            .OrderByDescending(l => l.StartedUtc)
            .Take(RecentRunCount)
            .Select(l => new RefreshRunDto(
                l.Id,
                l.TopicId,
                db.Topics.Where(t => t.Id == l.TopicId).Select(t => t.Name).FirstOrDefault(),
                l.JobType,
                l.Source,
                l.Status,
                l.StartedUtc,
                l.FinishedUtc,
                l.PostsProcessed,
                l.ApiCalls,
                l.Message))
            .ToListAsync(cancellationToken);

        return new SystemStatusDto(
            source,
            DataFreshnessBuilder.SourceLabel(source),
            options.Value.AllowSampleData,
            new QuotaStatusDto(
                ApiUsagePeriod.PeriodKey(now),
                usage?.PostsRead ?? 0,
                usage?.Requests ?? 0,
                settings.MonthlyPostReadBudget,
                decision.BudgetUsedPercent,
                decision.Mode,
                decision.Reason,
                usage?.RateLimitRemaining,
                usage?.RateLimitResetUtc),
            runs,
            now);
    }
}
