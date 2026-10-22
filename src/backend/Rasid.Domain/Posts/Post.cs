using Rasid.Domain.Common;
using Rasid.Domain.Engagement;

namespace Rasid.Domain.Posts;

/// <summary>
/// A public post collected from a data source. Stored once, even when it matches several topics,
/// so there is a single point of deletion for compliance.
/// </summary>
public sealed class Post : Entity<string>
{
    private Post()
    {
    }

    public string AuthorId { get; private set; } = string.Empty;

    public Author? Author { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public string? Language { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Link to the original post on X; null for sample data, which has no real post.</summary>
    public string? Url { get; private set; }

    public DataSource Source { get; private set; }

    /// <summary>Latest known metrics (denormalised for fast reads; history lives in snapshots).</summary>
    public EngagementMetrics Metrics { get; private set; } = EngagementMetrics.Empty;

    public DateTimeOffset MetricsCapturedUtc { get; private set; }

    public DateTimeOffset FirstSeenUtc { get; private set; }

    /// <summary>Last time the source confirmed the post still exists and is public.</summary>
    public DateTimeOffset LastValidatedUtc { get; private set; }

    /// <summary>After this moment the post content must be purged (retention policy).</summary>
    public DateTimeOffset ContentExpiresUtc { get; private set; }

    public string? MediaKind { get; private set; }

    public string? MediaPreviewUrl { get; private set; }

    public string? MediaAltText { get; private set; }

    public PostMedia? Media => MediaKind is null || MediaPreviewUrl is null
        ? null
        : new PostMedia(MediaKind, MediaPreviewUrl, MediaAltText);

    public static Post Create(
        string id,
        string authorId,
        string text,
        string? language,
        DateTimeOffset createdAtUtc,
        string? url,
        DataSource source,
        EngagementMetrics metrics,
        DateTimeOffset nowUtc,
        TimeSpan contentRetention,
        PostMedia? media = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorId);
        ArgumentNullException.ThrowIfNull(metrics);

        if (contentRetention <= TimeSpan.Zero)
        {
            throw new DomainException("Content retention must be positive.");
        }

        var post = new Post
        {
            Id = id,
            AuthorId = authorId,
            Text = text ?? string.Empty,
            Language = language,
            CreatedAtUtc = createdAtUtc,
            Url = url,
            Source = source,
            Metrics = metrics,
            MetricsCapturedUtc = nowUtc,
            FirstSeenUtc = nowUtc,
            LastValidatedUtc = nowUtc,
            ContentExpiresUtc = createdAtUtc + contentRetention,
        };
        post.SetMedia(media);
        return post;
    }

    /// <summary>Applies fresh data from the source; also confirms the post is still available.</summary>
    public void UpdateMetrics(EngagementMetrics metrics, DateTimeOffset nowUtc, PostMedia? media = null)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        Metrics = metrics;
        MetricsCapturedUtc = nowUtc;
        LastValidatedUtc = nowUtc;
        if (media is not null)
        {
            SetMedia(media);
        }
    }

    private void SetMedia(PostMedia? media)
    {
        MediaKind = media?.Kind;
        MediaPreviewUrl = media?.PreviewUrl;
        MediaAltText = media?.AltText;
    }

    public bool IsContentExpired(DateTimeOffset nowUtc) => nowUtc >= ContentExpiresUtc;
}
