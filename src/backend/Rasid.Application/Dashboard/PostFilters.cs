using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;

namespace Rasid.Application.Dashboard;

/// <summary>
/// Reusable, SQL-translatable post filters and sorts — shared by the dashboard and the posts list so
/// both always agree. Level filters use integer arithmetic (rate % = interactions × 100 ÷ views),
/// compared as interactions × 10,000 vs threshold × 100 × views, to stay exact in SQL.
/// </summary>
internal static class PostFilters
{
    public static IQueryable<Post> ForTopic(this IQueryable<Post> posts, IQueryable<TopicPost> links, Guid topicId) =>
        posts.Where(p => links.Any(tp => tp.TopicId == topicId && tp.PostId == p.Id));

    public static IQueryable<Post> CreatedBetween(this IQueryable<Post> posts, DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        posts.Where(p => p.CreatedAtUtc >= fromUtc && p.CreatedAtUtc <= toUtc);

    public static IQueryable<Post> ContainingKeyword(this IQueryable<Post> posts, string? keyword) =>
        string.IsNullOrWhiteSpace(keyword) ? posts : posts.Where(p => p.Text.Contains(keyword.Trim()));

    public static IQueryable<Post> WithLevel(this IQueryable<Post> posts, EngagementLevel? level, EngagementThresholds thresholds)
    {
        if (level is null)
        {
            return posts;
        }

        var medium = (long)(thresholds.MediumFromPercent * 100m);
        var high = (long)(thresholds.HighAbovePercent * 100m);

        return level switch
        {
            EngagementLevel.Unknown => posts.Where(p => p.Metrics.Views == null || p.Metrics.Views == 0),
            EngagementLevel.High => posts.Where(p => p.Metrics.Views > 0
                && (p.Metrics.Likes + p.Metrics.Reposts + p.Metrics.Replies + p.Metrics.Quotes) * 10_000 > high * p.Metrics.Views),
            EngagementLevel.Medium => posts.Where(p => p.Metrics.Views > 0
                && (p.Metrics.Likes + p.Metrics.Reposts + p.Metrics.Replies + p.Metrics.Quotes) * 10_000 >= medium * p.Metrics.Views
                && (p.Metrics.Likes + p.Metrics.Reposts + p.Metrics.Replies + p.Metrics.Quotes) * 10_000 <= high * p.Metrics.Views),
            _ => posts.Where(p => p.Metrics.Views > 0
                && (p.Metrics.Likes + p.Metrics.Reposts + p.Metrics.Replies + p.Metrics.Quotes) * 10_000 < medium * p.Metrics.Views),
        };
    }

    public static IOrderedQueryable<Post> SortBy(this IQueryable<Post> posts, PostSort sort, EngagementType type) => sort switch
    {
        PostSort.HighestRate => posts
            .OrderByDescending(p => p.Metrics.Views > 0
                ? (double)(p.Metrics.Likes + p.Metrics.Reposts + p.Metrics.Replies + p.Metrics.Quotes) / p.Metrics.Views
                : -1d)
            .ThenByDescending(p => p.CreatedAtUtc),
        PostSort.MostEngaged => type switch
        {
            EngagementType.Likes => posts.OrderByDescending(p => p.Metrics.Likes).ThenByDescending(p => p.CreatedAtUtc),
            EngagementType.Reposts => posts.OrderByDescending(p => p.Metrics.Reposts).ThenByDescending(p => p.CreatedAtUtc),
            EngagementType.Replies => posts.OrderByDescending(p => p.Metrics.Replies).ThenByDescending(p => p.CreatedAtUtc),
            EngagementType.Quotes => posts.OrderByDescending(p => p.Metrics.Quotes).ThenByDescending(p => p.CreatedAtUtc),
            _ => posts
                .OrderByDescending(p => p.Metrics.Likes + p.Metrics.Reposts + p.Metrics.Replies + p.Metrics.Quotes)
                .ThenByDescending(p => p.CreatedAtUtc),
        },
        _ => posts.OrderByDescending(p => p.CreatedAtUtc),
    };

    public static IQueryable<PostDto> ToDtos(this IQueryable<Post> posts) => posts.Select(p => new PostDto(
        p.Id,
        p.Text,
        p.Language,
        p.CreatedAtUtc,
        p.Url,
        p.Source,
        new AuthorDto(p.Author!.Id, p.Author.Username, p.Author.DisplayName, p.Author.ProfileImageUrl, p.Author.IsVerified),
        new MetricsDto(
            p.Metrics.Likes,
            p.Metrics.Reposts,
            p.Metrics.Replies,
            p.Metrics.Quotes,
            p.Metrics.Views,
            p.Metrics.Likes + p.Metrics.Reposts + p.Metrics.Replies + p.Metrics.Quotes),
        null,
        EngagementLevel.Unknown,
        p.MetricsCapturedUtc,
        p.MediaKind == null || p.MediaPreviewUrl == null ? null : new MediaDto(p.MediaKind, p.MediaPreviewUrl, p.MediaAltText)));

    /// <summary>Rates and levels are computed in C# with the single domain formula, after the SQL projection.</summary>
    public static PostDto WithEngagement(this PostDto post, EngagementThresholds thresholds)
    {
        var metrics = new EngagementMetrics(post.Metrics.Likes, post.Metrics.Reposts, post.Metrics.Replies, post.Metrics.Quotes, post.Metrics.Views);
        return post with
        {
            EngagementRate = EngagementCalculator.Rate(metrics),
            EngagementLevel = EngagementCalculator.Level(metrics, thresholds),
        };
    }
}
