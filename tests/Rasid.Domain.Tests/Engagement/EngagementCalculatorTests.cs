using Rasid.Domain.Common;
using Rasid.Domain.Engagement;

namespace Rasid.Domain.Tests.Engagement;

public sealed class EngagementCalculatorTests
{
    [Fact]
    public void Rate_is_interactions_over_views_as_percentage()
    {
        // 120 + 30 + 10 + 5 = 165 interactions over 6,000 views = 2.75 %
        var metrics = new EngagementMetrics(likes: 120, reposts: 30, replies: 10, quotes: 5, views: 6_000);

        Assert.Equal(2.75m, EngagementCalculator.Rate(metrics));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void Rate_is_not_available_when_views_are_missing_or_zero(long? views)
    {
        var metrics = new EngagementMetrics(10, 1, 1, 0, views);

        Assert.Null(EngagementCalculator.Rate(metrics));
        Assert.Equal(EngagementLevel.Unknown, EngagementCalculator.Level(metrics, EngagementThresholds.Default));
    }

    [Theory]
    [InlineData(0.99, EngagementLevel.Low)]
    [InlineData(1.00, EngagementLevel.Medium)]
    [InlineData(3.00, EngagementLevel.Medium)]
    [InlineData(3.01, EngagementLevel.High)]
    public void Level_follows_documented_thresholds(double rate, EngagementLevel expected)
    {
        Assert.Equal(expected, EngagementCalculator.Level((decimal)rate, EngagementThresholds.Default));
    }

    [Fact]
    public void Summary_uses_view_weighted_rate_and_excludes_posts_without_views()
    {
        // Worked example from docs/06-engagement-metrics.md §5
        var summary = EngagementCalculator.Summarize(
        [
            new EngagementMetrics(120, 30, 10, 5, 6_000),
            new EngagementMetrics(8, 1, 0, 0, 2_000),
            new EngagementMetrics(40, 15, 6, 2, null),
        ]);

        Assert.Equal(3, summary.Posts);
        Assert.Equal(237, summary.Interactions);
        Assert.Equal(2, summary.ViewedPosts);
        Assert.Equal(8_000, summary.Views);
        Assert.Equal(2.18m, summary.Rate); // (165 + 9) / 8,000
    }

    [Fact]
    public void Summary_of_no_posts_has_no_rate()
    {
        var summary = EngagementCalculator.Summarize([]);

        Assert.Equal(0, summary.Posts);
        Assert.Null(summary.Rate);
    }

    [Theory]
    [InlineData(EngagementType.All, 16)]
    [InlineData(EngagementType.Likes, 10)]
    [InlineData(EngagementType.Reposts, 3)]
    [InlineData(EngagementType.Replies, 2)]
    [InlineData(EngagementType.Quotes, 1)]
    public void ValueOf_selects_the_requested_engagement_type(EngagementType type, long expected)
    {
        var metrics = new EngagementMetrics(10, 3, 2, 1, 100);

        Assert.Equal(expected, EngagementCalculator.ValueOf(metrics, type));
    }

    [Fact]
    public void Summary_with_thresholds_counts_posts_per_level()
    {
        var summary = EngagementCalculator.Summarize(
            [
                new EngagementMetrics(120, 30, 10, 5, 6_000), // 2.75 % → Medium
                new EngagementMetrics(8, 1, 0, 0, 2_000),     // 0.45 % → Low
                new EngagementMetrics(50, 10, 5, 5, 1_000),   // 7.00 % → High
                new EngagementMetrics(40, 15, 6, 2, null),    // N/A    → Unknown
            ],
            EngagementThresholds.Default);

        Assert.Equal(new LevelCounts(High: 1, Medium: 1, Low: 1, Unknown: 1), summary.Levels);
    }

    [Fact]
    public void Summaries_can_be_added_for_rollups()
    {
        var a = EngagementCalculator.Summarize([new EngagementMetrics(10, 0, 0, 0, 100)], EngagementThresholds.Default);
        var b = EngagementCalculator.Summarize([new EngagementMetrics(5, 5, 0, 0, null)], EngagementThresholds.Default);

        var total = a.Add(b);

        Assert.Equal(2, total.Posts);
        Assert.Equal(20, total.Interactions);
        Assert.Equal(10m, total.Rate); // only the viewed post counts: 10 / 100
        Assert.Equal(new LevelCounts(1, 0, 0, 1), total.Levels);
    }

    [Fact]
    public void Negative_counters_are_rejected()
    {
        Assert.Throws<DomainException>(() => new EngagementMetrics(-1, 0, 0, 0, 0));
    }

    [Fact]
    public void Thresholds_must_be_ordered()
    {
        Assert.Throws<DomainException>(() => new EngagementThresholds(3m, 1m));
    }
}
