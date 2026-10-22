namespace Rasid.Domain.Engagement;

/// <summary>
/// Totals over a set of posts. <see cref="ViewedPosts"/> and <see cref="ViewedInteractions"/> cover only
/// posts that have view counts, so the rate is never diluted or inflated by posts without views.
/// </summary>
public sealed record EngagementSummary(
    long Posts,
    long Likes,
    long Reposts,
    long Replies,
    long Quotes,
    long Views,
    long ViewedPosts,
    long ViewedInteractions)
{
    public long Interactions => Likes + Reposts + Replies + Quotes;

    /// <summary>View-weighted topic engagement rate in percent; null when no post has views.</summary>
    public decimal? Rate => EngagementCalculator.WeightedRate(ViewedInteractions, Views);

    /// <summary>Posts per engagement level; present when the summary was built with thresholds.</summary>
    public LevelCounts? Levels { get; init; }

    public static EngagementSummary Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0) { Levels = LevelCounts.None };

    /// <summary>Adds two summaries (e.g. hourly buckets into a daily or overall total).</summary>
    public EngagementSummary Add(EngagementSummary other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new EngagementSummary(
            Posts + other.Posts,
            Likes + other.Likes,
            Reposts + other.Reposts,
            Replies + other.Replies,
            Quotes + other.Quotes,
            Views + other.Views,
            ViewedPosts + other.ViewedPosts,
            ViewedInteractions + other.ViewedInteractions)
        {
            Levels = (Levels ?? LevelCounts.None).Add(other.Levels ?? LevelCounts.None),
        };
    }
}

/// <summary>Number of posts per engagement level.</summary>
public sealed record LevelCounts(long High, long Medium, long Low, long Unknown)
{
    public static LevelCounts None { get; } = new(0, 0, 0, 0);

    public LevelCounts Add(LevelCounts other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(High + other.High, Medium + other.Medium, Low + other.Low, Unknown + other.Unknown);
    }
}
