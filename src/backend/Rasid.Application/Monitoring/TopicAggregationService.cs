using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rasid.Application.Common.Abstractions;
using Rasid.Application.Common.Options;
using Rasid.Domain.Engagement;

namespace Rasid.Application.Monitoring;

/// <summary>
/// Maintains the hourly read model (<see cref="TopicMetricsHourly"/>) that dashboards query (CQRS-lite).
/// Recomputes only the affected hour range, inside a transaction, so readers never see a partial state.
/// </summary>
public sealed class TopicAggregationService(IRasidDbContext db, IOptions<MonitoringOptions> options, TimeProvider clock)
{
    public async Task RecomputeAsync(Guid topicId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        var from = TopicMetricsHourly.TruncateToHour(fromUtc);
        var to = TopicMetricsHourly.TruncateToHour(toUtc).AddHours(1);
        var thresholds = options.Value.Engagement.ToThresholds();

        var posts = await db.TopicPosts
            .AsNoTracking()
            .Where(tp => tp.TopicId == topicId && tp.Post!.CreatedAtUtc >= from && tp.Post.CreatedAtUtc < to)
            .Select(tp => new
            {
                tp.Post!.CreatedAtUtc,
                tp.Post.Metrics.Likes,
                tp.Post.Metrics.Reposts,
                tp.Post.Metrics.Replies,
                tp.Post.Metrics.Quotes,
                tp.Post.Metrics.Views,
            })
            .ToListAsync(cancellationToken);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var hourly = posts
                .GroupBy(p => TopicMetricsHourly.TruncateToHour(p.CreatedAtUtc))
                .Select(hour => TopicMetricsHourly.From(
                    topicId,
                    hour.Key,
                    EngagementCalculator.Summarize(
                        hour.Select(p => new EngagementMetrics(p.Likes, p.Reposts, p.Replies, p.Quotes, p.Views)),
                        thresholds),
                    now))
                .ToList();

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await db.TopicMetricsHourly
                .Where(h => h.TopicId == topicId && h.HourUtc >= from && h.HourUtc < to)
                .ExecuteDeleteAsync(cancellationToken);

            db.TopicMetricsHourly.AddRange(hourly);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
