using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Options;
using Rasid.Application.Connectors;
using Rasid.Domain.Common;
using Rasid.Domain.Posts;
using Rasid.Domain.Refresh;
using Rasid.Domain.Topics;

namespace Rasid.Application.Monitoring;

/// <summary>
/// Discovery: collects new posts for one topic. Flow documented in docs/03-flowcharts.md §3.
/// <list type="number">
/// <item>Quota check (may defer)</item>
/// <item>Incremental search from the last seen post, page by page within the page budget</item>
/// <item>Upsert posts, authors and snapshots; link posts to the topic</item>
/// <item>Record outcome on the topic and in the refresh log; update aggregates</item>
/// </list>
/// </summary>
public sealed partial class TopicIngestionService(
    IRasidDbContext db,
    IPostSourceResolver sourceResolver,
    IQuotaManager quota,
    PostWriter postWriter,
    TopicAggregationService aggregation,
    IDistributedLock writeLock,
    IOptions<MonitoringOptions> options,
    TimeProvider clock,
    ILogger<TopicIngestionService> logger)
{
    private static readonly TimeSpan WriteLockTimeout = TimeSpan.FromSeconds(60);

    public async Task<RefreshStatus> IngestAsync(Guid topicId, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.Include(t => t.Rules).FirstOrDefaultAsync(t => t.Id == topicId, cancellationToken);
        if (topic is null || !topic.IsActive)
        {
            LogSkipped(topicId);
            return RefreshStatus.Pending;
        }

        var source = sourceResolver.Resolve();
        if (source is null)
        {
            return await CompleteAsync(topic, null, RefreshStatus.NotConfigured, 0, 0, null,
                "No data source is configured for this environment.", cancellationToken);
        }

        var decision = await quota.EvaluateAsync(source.Source, topic.Priority, cancellationToken);
        if (!decision.IsAllowed)
        {
            return await CompleteAsync(topic, source.Source, RefreshStatus.Deferred, 0, 0, decision.RetryAfterUtc,
                decision.Reason, cancellationToken);
        }

        var run = await CollectAsync(topic, source, decision.MaxPages, cancellationToken);

        var status = run.Failure is not null
            ? run.Failure.ToRefreshStatus()
            : run.NewLinks == 0 && !await db.TopicPosts.AnyAsync(tp => tp.TopicId == topic.Id, cancellationToken)
                ? RefreshStatus.NoResults
                : RefreshStatus.Success;

        // Advance the cursor only when the run reached the end of the result set, or stopped because of the
        // page budget. On a mid-run failure, unread pages would otherwise be skipped forever.
        var newestPostId = run.Failure is null ? run.NewestPostId : null;

        var result = await CompleteAsync(topic, source.Source, status, run.PostsRead, run.ApiCalls,
            run.Failure?.RetryAfterUtc, run.Failure?.Message ?? run.Note, cancellationToken, newestPostId);

        if (run.OldestCreatedUtc is { } from && run.NewestCreatedUtc is { } to)
        {
            await aggregation.RecomputeAsync(topic.Id, from, to, cancellationToken);
        }

        return result;
    }

    private async Task<CollectionRun> CollectAsync(
        Topic topic, IPostSource source, int maxPages, CancellationToken cancellationToken)
    {
        var run = new CollectionRun();
        var matchTerms = topic.Rules.Where(r => !r.IsExclusion).Select(r => r.DisplayValue).ToList();
        var sinceId = topic.IncrementalCursor(source.Source, clock.GetUtcNow(), source.MaxCursorAge);
        string? paginationToken = null;

        for (var page = 0; page < maxPages; page++)
        {
            var request = new SourceSearchRequest(
                topic.GeneratedQuery, matchTerms, sinceId, options.Value.PageSize, paginationToken);

            var response = await source.SearchRecentAsync(request, cancellationToken);
            run.ApiCalls++;

            if (!response.IsSuccess)
            {
                run.Failure = response.Failure;
                if (response.Failure!.Kind == SourceFailureKind.RateLimited && response.Failure.RetryAfterUtc is { } reset)
                {
                    await quota.RecordRateLimitedAsync(source.Source, reset, cancellationToken);
                }

                LogSourceFailure(topic.Id, response.Failure.Kind, response.Failure.Message);
                break;
            }

            var result = response.Value!;

            // Only the database writes are serialised; the X API call above runs in parallel across topics.
            using (await writeLock.AcquireAsync(LockNames.PostWrites, WriteLockTimeout, cancellationToken))
            {
                var now = clock.GetUtcNow();
                var posts = await postWriter.UpsertAsync(result.Posts, result.Authors, source.Source, now, cancellationToken);
                run.NewLinks += await LinkAsync(topic.Id, posts, now, cancellationToken);
                run.Track(posts);
                await db.SaveChangesAsync(cancellationToken);
            }
            await quota.RecordUsageAsync(source.Source, result.Posts.Count, 1, result.RateLimit, cancellationToken);

            paginationToken = result.NextToken;
            if (paginationToken is null)
            {
                break;
            }

            if (page == maxPages - 1)
            {
                run.Note = "Page budget reached; older matching posts in this window were not collected.";
            }
        }

        return run;
    }

    private async Task<int> LinkAsync(Guid topicId, IReadOnlyList<Post> posts, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = posts.Select(p => p.Id).ToList();
        var alreadyLinked = await db.TopicPosts
            .Where(tp => tp.TopicId == topicId && ids.Contains(tp.PostId))
            .Select(tp => tp.PostId)
            .ToListAsync(cancellationToken);

        var newIds = ids.Except(alreadyLinked, StringComparer.Ordinal).ToList();
        db.TopicPosts.AddRange(newIds.Select(id => TopicPost.Link(topicId, id, now)));
        return newIds.Count;
    }

    private async Task<RefreshStatus> CompleteAsync(
        Topic topic,
        DataSource? source,
        RefreshStatus status,
        int postsProcessed,
        int apiCalls,
        DateTimeOffset? retryAfterUtc,
        string? message,
        CancellationToken cancellationToken,
        string? newestPostId = null)
    {
        var now = clock.GetUtcNow();
        var log = RefreshLog.Start(topic.Id, RefreshJobType.Discovery, source, now);
        log.Complete(status, now, postsProcessed, apiCalls, retryAfterUtc, message);
        db.RefreshLogs.Add(log);

        topic.RecordRefresh(status, now, newestPostId, source);
        await db.SaveChangesAsync(cancellationToken);

        LogCompleted(topic.Id, status, postsProcessed, apiCalls);
        return status;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovery skipped: topic {TopicId} not found or paused")]
    private partial void LogSkipped(Guid topicId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Discovery for topic {TopicId} failed: {Kind} — {Reason}")]
    private partial void LogSourceFailure(Guid topicId, SourceFailureKind kind, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovery for topic {TopicId} finished: {Status}, {Posts} posts, {Calls} API calls")]
    private partial void LogCompleted(Guid topicId, RefreshStatus status, int posts, int calls);

    private sealed class CollectionRun
    {
        public int ApiCalls { get; set; }

        public int PostsRead { get; private set; }

        public int NewLinks { get; set; }

        public SourceFailure? Failure { get; set; }

        public string? Note { get; set; }

        public string? NewestPostId { get; private set; }

        public DateTimeOffset? OldestCreatedUtc { get; private set; }

        public DateTimeOffset? NewestCreatedUtc { get; private set; }

        public void Track(IReadOnlyList<Post> posts)
        {
            if (posts.Count == 0)
            {
                return;
            }

            PostsRead += posts.Count;

            var newest = PostId.Newest(posts.Select(p => p.Id));
            if (newest is not null && PostId.IsNewer(newest, NewestPostId))
            {
                NewestPostId = newest;
            }

            var oldest = posts.Min(p => p.CreatedAtUtc);
            var latest = posts.Max(p => p.CreatedAtUtc);
            OldestCreatedUtc = OldestCreatedUtc is null || oldest < OldestCreatedUtc ? oldest : OldestCreatedUtc;
            NewestCreatedUtc = NewestCreatedUtc is null || latest > NewestCreatedUtc ? latest : NewestCreatedUtc;
        }
    }
}
