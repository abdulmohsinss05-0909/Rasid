using Rasid.Domain.Common;

namespace Rasid.Domain.Engagement;

/// <summary>
/// Public engagement counters of a post, as returned by X (<c>public_metrics</c>).
/// <see cref="Views"/> is nullable: X does not return impressions for every post or plan, and a
/// missing value must never be treated as zero.
/// </summary>
public sealed record EngagementMetrics
{
    public EngagementMetrics(long likes, long reposts, long replies, long quotes, long? views)
    {
        if (likes < 0 || reposts < 0 || replies < 0 || quotes < 0 || views < 0)
        {
            throw new DomainException("Engagement counters cannot be negative.");
        }

        Likes = likes;
        Reposts = reposts;
        Replies = replies;
        Quotes = quotes;
        Views = views;
    }

    public static EngagementMetrics Empty { get; } = new(0, 0, 0, 0, null);

    public long Likes { get; private init; }

    public long Reposts { get; private init; }

    public long Replies { get; private init; }

    public long Quotes { get; private init; }

    public long? Views { get; private init; }

    /// <summary>Likes + Reposts + Replies + Quotes.</summary>
    public long Interactions => Likes + Reposts + Replies + Quotes;

    public bool HasViews => Views is > 0;
}
