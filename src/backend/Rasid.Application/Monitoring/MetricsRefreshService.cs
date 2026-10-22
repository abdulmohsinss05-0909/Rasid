using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Options;
using Rasid.Application.Connectors;
using Rasid.Domain.Posts;
using Rasid.Domain.Refresh;
using Rasid.Domain.Topics;

namespace Rasid.Application.Monitoring;

/// <summary>
/// Tiered metric refresh (docs/03-flowcharts.md §6) and deletion sync (§9).
/// Young posts are refreshed often, older ones daily; posts the source no longer returns
/// (deleted, protected, suspended) are removed from Rasid immediately.
/// </summary>
public sealed partial class MetricsRefreshService(
    IRasidDbContext db,
    IPostSourceResolver sourceResolver,
    IQuotaManager quota,
    PostWriter postWriter,
    TopicAggregationService aggregation,
    IAuditTrail auditTrail,
    IDistributedLock writeLock,
    IOptions<MonitoringOptions> options,
    TimeProvider clock,
    ILogger<MetricsRefreshService> logger)
{
    private static readonly TimeSpan WriteLockTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan FreshPostAge = TimeSpan.FromHours(24);
    private static readonly TimeSpan RecentPostAge = TimeSpan.FromDays(3);

    public async Task<int> RefreshDueAsync(CancellationToken cancellationToken)
    {
        var source = sourceResolver.Resolve();
        if (source is null)
        {
            return 0;
        }

        var decision = await quota.EvaluateAsync(source.Source, TopicPriority.Normal, cancellationToken);
        if (!decision.IsAllowed)
        {
            LogDeferred(decision.Reason);
            return 0;
        }

        var dueIds = await SelectDuePostIdsAsync(source, cancellationToken);
        if (dueIds.Count == 0)
        {
            return 0;
        }

        var startedUtc = clock.GetUtcNow();
        var refreshed = new List<Post>();
        var unavailable = new List<string>();
        var apiCalls = 0;
        SourceFailure? failure = null;

        foreach (var batch in dueIds.Chunk(source.MaxLookupBatchSize))
        {
            var response = await source.LookupAsync(batch, cancellationToken);
            apiCalls++;

            if (!response.IsSuccess)
            {
                failure = response.Failure!;
                if (failure.Kind == SourceFailureKind.RateLimited && failure.RetryAfterUtc is { } reset)
                {
                    await quota.RecordRateLimitedAsync(source.Source, reset, cancellationToken);
                }

                break;
            }

            var lookup = response.Value!;
            using (await writeLock.AcquireAsync(LockNames.PostWrites, WriteLockTimeout, cancellationToken))
            {
                refreshed.AddRange(await postWriter.UpsertAsync(
                    lookup.Posts, lookup.Authors, source.Source, clock.GetUtcNow(), cancellationToken));
                await db.SaveChangesAsync(cancellationToken);
            }

            unavailable.AddRange(lookup.UnavailableIds);
            await quota.RecordUsageAsync(source.Source, lookup.Posts.Count, 1, lookup.RateLimit, cancellationToken);
        }

        var affectedIds = refreshed.Select(p => p.Id).Concat(unavailable).ToList();
        var affectedTopics = await db.TopicPosts
            .Where(tp => affectedIds.Contains(tp.PostId))
            .GroupBy(tp => tp.TopicId)
            .Select(g => new
            {
                TopicId = g.Key,
                From = g.Min(tp => tp.Post!.CreatedAtUtc),
                To = g.Max(tp => tp.Post!.CreatedAtUtc),
            })
            .ToListAsync(cancellationToken);

        if (unavailable.Count > 0)
        {
            await RemoveUnavailableAsync(unavailable, cancellationToken);
        }

        var log = RefreshLog.Start(null, RefreshJobType.MetricsRefresh, source.Source, startedUtc);
        log.Complete(
            failure?.ToRefreshStatus() ?? RefreshStatus.Success,
            clock.GetUtcNow(),
            refreshed.Count,
            apiCalls,
            failure?.RetryAfterUtc,
            failure?.Message ?? (unavailable.Count > 0 ? $"{unavailable.Count} unavailable post(s) removed." : null));
        db.RefreshLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var topic in affectedTopics)
        {
            await aggregation.RecomputeAsync(topic.TopicId, topic.From, topic.To, cancellationToken);
        }

        LogCompleted(refreshed.Count, unavailable.Count, apiCalls);
        return refreshed.Count;
    }

    private Task<List<string>> SelectDuePostIdsAsync(IPostSource source, CancellationToken cancellationToken)
    {
        var tiers = options.Value.MetricRefresh;
        var now = clock.GetUtcNow();
        var freshSince = now - FreshPostAge;
        var recentSince = now - RecentPostAge;
        var freshStaleBefore = now.AddMinutes(-tiers.FreshPostMinutes);
        var recentStaleBefore = now.AddMinutes(-tiers.RecentPostMinutes);
        var olderStaleBefore = now.AddMinutes(-tiers.OlderPostMinutes);

        return db.Posts
            .Where(p => p.Source == source.Source && p.ContentExpiresUtc > now)
            .Where(p =>
                (p.CreatedAtUtc >= freshSince && p.MetricsCapturedUtc <= freshStaleBefore)
                || (p.CreatedAtUtc < freshSince && p.CreatedAtUtc >= recentSince && p.MetricsCapturedUtc <= recentStaleBefore)
                || (p.CreatedAtUtc < recentSince && p.MetricsCapturedUtc <= olderStaleBefore))
            .OrderBy(p => p.MetricsCapturedUtc)
            .Select(p => p.Id)
            .Take(tiers.MaxPostsPerRun)
            .ToListAsync(cancellationToken);
    }

    private async Task RemoveUnavailableAsync(List<string> postIds, CancellationToken cancellationToken)
    {
        // Compliance: content deleted or made non-public on the source is removed (cascades to links and snapshots).
        var removed = await db.Posts.Where(p => postIds.Contains(p.Id)).ExecuteDeleteAsync(cancellationToken);

        auditTrail.Record(
            AuditActions.PostRemovedUnavailable,
            nameof(Post),
            entityId: null,
            details: $"{removed} post(s) no longer available at source were removed.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Metric refresh deferred by quota policy: {Reason}")]
    private partial void LogDeferred(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Metric refresh finished: {Refreshed} refreshed, {Removed} removed, {Calls} API calls")]
    private partial void LogCompleted(int refreshed, int removed, int calls);
}
