using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Options;
using Rasid.Application.Connectors;
using Rasid.Domain.Common;
using Rasid.Domain.Refresh;
using Rasid.Domain.Topics;

namespace Rasid.Application.Dashboard;

/// <summary>
/// Builds the "where does this data come from and how fresh is it" block shown on every dashboard,
/// including a clear message for each unavailable/failed state (docs/03-flowcharts.md §8).
/// </summary>
internal sealed class DataFreshnessBuilder(
    IRasidDbContext db,
    IPostSourceResolver sourceResolver,
    IOptions<MonitoringOptions> options,
    TimeProvider clock)
{
    public async Task<DataFreshnessDto> BuildAsync(Topic topic, CancellationToken cancellationToken)
    {
        var lastRun = await db.RefreshLogs
            .AsNoTracking()
            .Where(l => l.TopicId == topic.Id && l.JobType == RefreshJobType.Discovery)
            .OrderByDescending(l => l.StartedUtc)
            .Select(l => new { l.Source, l.RetryAfterUtc })
            .FirstOrDefaultAsync(cancellationToken);

        var now = clock.GetUtcNow();
        var source = lastRun?.Source ?? sourceResolver.Resolve()?.Source;
        var isStale = topic.IsStale(now, options.Value.RefreshInterval(topic.Priority));

        return new DataFreshnessDto(
            source,
            SourceLabel(source),
            source == DataSource.Sample,
            topic.LastRefreshStatus,
            isStale,
            topic.LastSuccessfulRefreshUtc,
            topic.LastRefreshAttemptUtc,
            lastRun?.RetryAfterUtc,
            Message(topic.LastRefreshStatus, isStale, topic.IsActive),
            now);
    }

    public static string SourceLabel(DataSource? source) => source switch
    {
        DataSource.XApi => "Live — X API v2",
        DataSource.Sample => "SAMPLE DATA — fictitious posts for demonstration",
        _ => "No data source configured",
    };

    private static string Message(RefreshStatus status, bool isStale, bool isActive)
    {
        if (!isActive)
        {
            return "Collection is paused for this topic. Showing the last collected data.";
        }

        return status switch
        {
            RefreshStatus.Pending => "Collecting data for this topic. Results will appear shortly.",
            RefreshStatus.Success when isStale => "Data may be out of date: the latest scheduled update has not completed yet.",
            RefreshStatus.Success => "Data is up to date.",
            RefreshStatus.NoResults => "No public posts matched this topic in the searched period.",
            RefreshStatus.Deferred => "Update postponed to protect the X API budget. Showing the last available data.",
            RefreshStatus.RateLimited => "X API rate limit reached. Showing the last available data until the limit resets.",
            RefreshStatus.AuthFailed => "The data source rejected the credentials. Administrator action is required. Showing the last available data.",
            RefreshStatus.ConnectionFailed => "The data source could not be reached. Showing the last available data; the next attempt is automatic.",
            RefreshStatus.NotConfigured => "No data source is configured for this environment.",
            _ => "Status unknown.",
        };
    }
}
