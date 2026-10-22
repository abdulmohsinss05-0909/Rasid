using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Connectors;
using Rasid.Domain.Common;
using Rasid.Domain.Posts;

namespace Rasid.Application.Monitoring;

/// <summary>
/// Makes "switch to live X data" a configuration-only change. Once the X connector is active, any
/// fictitious sample posts are removed (with the aggregates built from them and the sample cursors),
/// so dashboards never mix demonstration data with real data. Real X data is never removed here.
/// </summary>
public sealed partial class SourceTransitionService(
    IRasidDbContext db,
    IPostSourceResolver sourceResolver,
    IAuditTrail auditTrail,
    ILogger<SourceTransitionService> logger)
{
    public async Task<int> RemoveSampleDataWhenLiveAsync(CancellationToken cancellationToken)
    {
        if (sourceResolver.Resolve()?.Source != DataSource.XApi
            || !await db.Posts.AnyAsync(p => p.Source == DataSource.Sample, cancellationToken))
        {
            return 0;
        }

        var affectedTopics = await db.TopicPosts
            .Where(tp => tp.Post!.Source == DataSource.Sample)
            .Select(tp => tp.TopicId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var strategy = db.Database.CreateExecutionStrategy();
        var removed = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var posts = await db.Posts.Where(p => p.Source == DataSource.Sample).ExecuteDeleteAsync(cancellationToken);
            await db.Authors.Where(a => !db.Posts.Any(p => p.AuthorId == a.Id)).ExecuteDeleteAsync(cancellationToken);

            // Aggregates of these topics contained sample data; they are rebuilt from live posts on the next runs.
            await db.TopicMetricsHourly.Where(h => affectedTopics.Contains(h.TopicId)).ExecuteDeleteAsync(cancellationToken);

            var topics = await db.Topics.Where(t => t.CursorSource == DataSource.Sample).ToListAsync(cancellationToken);
            topics.ForEach(t => t.ResetCursor());

            auditTrail.Record(AuditActions.SampleDataRemoved, nameof(Post), entityId: null,
                details: $"Live X source active: {posts} sample post(s) removed from {affectedTopics.Count} topic(s).");
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return posts;
        });

        LogSampleRemoved(removed, affectedTopics.Count);
        return removed;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Live X source active: removed {Posts} sample post(s) from {Topics} topic(s)")]
    private partial void LogSampleRemoved(int posts, int topics);
}
