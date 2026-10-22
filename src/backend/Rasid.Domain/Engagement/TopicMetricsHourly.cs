namespace Rasid.Domain.Engagement;

/// <summary>
/// Pre-aggregated read model: one row per topic per hour (by post creation time).
/// Dashboards read this table instead of aggregating raw posts (CQRS-lite).
/// </summary>
public sealed class TopicMetricsHourly
{
    private TopicMetricsHourly()
    {
    }

    public Guid TopicId { get; private set; }

    public DateTimeOffset HourUtc { get; private set; }

    public long Posts { get; private set; }

    public long Likes { get; private set; }

    public long Reposts { get; private set; }

    public long Replies { get; private set; }

    public long Quotes { get; private set; }

    public long Views { get; private set; }

    public long ViewedPosts { get; private set; }

    public long ViewedInteractions { get; private set; }

    public long HighPosts { get; private set; }

    public long MediumPosts { get; private set; }

    public long LowPosts { get; private set; }

    public long UnknownPosts { get; private set; }

    public DateTimeOffset ComputedUtc { get; private set; }

    public static TopicMetricsHourly From(
        Guid topicId, DateTimeOffset hourUtc, EngagementSummary summary, DateTimeOffset computedUtc)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var levels = summary.Levels ?? LevelCounts.None;
        return new TopicMetricsHourly
        {
            TopicId = topicId,
            HourUtc = TruncateToHour(hourUtc),
            Posts = summary.Posts,
            Likes = summary.Likes,
            Reposts = summary.Reposts,
            Replies = summary.Replies,
            Quotes = summary.Quotes,
            Views = summary.Views,
            ViewedPosts = summary.ViewedPosts,
            ViewedInteractions = summary.ViewedInteractions,
            HighPosts = levels.High,
            MediumPosts = levels.Medium,
            LowPosts = levels.Low,
            UnknownPosts = levels.Unknown,
            ComputedUtc = computedUtc,
        };
    }

    public EngagementSummary ToSummary() =>
        new(Posts, Likes, Reposts, Replies, Quotes, Views, ViewedPosts, ViewedInteractions)
        {
            Levels = new LevelCounts(HighPosts, MediumPosts, LowPosts, UnknownPosts),
        };

    public static DateTimeOffset TruncateToHour(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }
}
