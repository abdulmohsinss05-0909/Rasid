namespace Rasid.Domain.Engagement;

/// <summary>
/// The engagement formulas, in one place. Documented in docs/06-engagement-metrics.md.
/// <list type="bullet">
/// <item>Interactions = Likes + Reposts + Replies + Quotes</item>
/// <item>Post rate (%) = Interactions ÷ Views × 100 — N/A when views are missing or zero</item>
/// <item>Topic rate (%) = Σ Interactions ÷ Σ Views, over posts that have views (view-weighted)</item>
/// </list>
/// </summary>
public static class EngagementCalculator
{
    private const int RateDecimals = 2;

    public static decimal? Rate(EngagementMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        return WeightedRate(metrics.Interactions, metrics.Views ?? 0);
    }

    /// <summary>Rate from totals. Returns null (N/A) when there are no views to divide by.</summary>
    public static decimal? WeightedRate(long interactionsOfViewedPosts, long views) =>
        views > 0
            ? Math.Round(interactionsOfViewedPosts * 100m / views, RateDecimals, MidpointRounding.AwayFromZero)
            : null;

    public static EngagementLevel Level(decimal? ratePercent, EngagementThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        return ratePercent switch
        {
            null => EngagementLevel.Unknown,
            var r when r > thresholds.HighAbovePercent => EngagementLevel.High,
            var r when r >= thresholds.MediumFromPercent => EngagementLevel.Medium,
            _ => EngagementLevel.Low,
        };
    }

    /// <summary>
    /// Level of a single post, from the exact (unrounded) rate so it always agrees with the
    /// SQL level filter used for listing posts.
    /// </summary>
    public static EngagementLevel Level(EngagementMetrics metrics, EngagementThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return metrics.HasViews
            ? Level(metrics.Interactions * 100m / metrics.Views!.Value, thresholds)
            : EngagementLevel.Unknown;
    }

    /// <summary>The counter that corresponds to an engagement type (All = total interactions).</summary>
    public static long ValueOf(EngagementMetrics metrics, EngagementType type)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return type switch
        {
            EngagementType.Likes => metrics.Likes,
            EngagementType.Reposts => metrics.Reposts,
            EngagementType.Replies => metrics.Replies,
            EngagementType.Quotes => metrics.Quotes,
            _ => metrics.Interactions,
        };
    }

    /// <summary>Topic-level summary over a set of posts.</summary>
    public static EngagementSummary Summarize(IEnumerable<EngagementMetrics> posts)
    {
        ArgumentNullException.ThrowIfNull(posts);

        var summary = new EngagementSummaryBuilder();
        foreach (var metrics in posts)
        {
            summary.Add(metrics);
        }

        return summary.Build();
    }

    /// <summary>Topic-level summary including how many posts fall into each engagement level.</summary>
    public static EngagementSummary Summarize(IEnumerable<EngagementMetrics> posts, EngagementThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(posts);
        ArgumentNullException.ThrowIfNull(thresholds);

        var summary = new EngagementSummaryBuilder(thresholds);
        foreach (var metrics in posts)
        {
            summary.Add(metrics);
        }

        return summary.Build();
    }

    private sealed class EngagementSummaryBuilder(EngagementThresholds? thresholds = null)
    {
        private long _posts;
        private long _likes;
        private long _reposts;
        private long _replies;
        private long _quotes;
        private long _views;
        private long _viewedPosts;
        private long _viewedInteractions;
        private long _high;
        private long _medium;
        private long _low;

        public void Add(EngagementMetrics m)
        {
            _posts++;
            _likes += m.Likes;
            _reposts += m.Reposts;
            _replies += m.Replies;
            _quotes += m.Quotes;

            if (m.HasViews)
            {
                _views += m.Views!.Value;
                _viewedPosts++;
                _viewedInteractions += m.Interactions;
            }

            if (thresholds is null)
            {
                return;
            }

            switch (Level(m, thresholds))
            {
                case EngagementLevel.High:
                    _high++;
                    break;
                case EngagementLevel.Medium:
                    _medium++;
                    break;
                case EngagementLevel.Low:
                    _low++;
                    break;
            }
        }

        public EngagementSummary Build() => new(
            _posts, _likes, _reposts, _replies, _quotes, _views, _viewedPosts, _viewedInteractions)
        {
            Levels = thresholds is null ? null : new LevelCounts(_high, _medium, _low, _posts - _high - _medium - _low),
        };
    }
}
