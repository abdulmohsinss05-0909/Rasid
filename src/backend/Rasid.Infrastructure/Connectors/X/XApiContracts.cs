using System.Text.Json;

namespace Rasid.Infrastructure.Connectors.X;

// Wire contracts for the X API v2 responses we consume. Only the fields we request are modelled
// (data minimisation). Property names map via the snake_case naming policy in XJson.Options.
// X's documentation is migrating "tweet" names to "post" names (retweet_count → repost_count,
// note_tweet → note_post); both spellings are modelled so either response shape is read correctly.

internal sealed record XTweetsResponse(
    IReadOnlyList<XTweet>? Data,
    XIncludes? Includes,
    XMeta? Meta,
    IReadOnlyList<XProblem>? Errors);

internal sealed record XTweet(
    string Id,
    string Text,
    string AuthorId,
    DateTimeOffset CreatedAt,
    string? Lang,
    XPublicMetrics? PublicMetrics,
    XLongText? NoteTweet,
    XLongText? NotePost,
    XAttachments? Attachments)
{
    /// <summary>Full text: long-form posts carry it in note_tweet / note_post; the top-level text is truncated.</summary>
    public string FullText => NoteTweet?.Text ?? NotePost?.Text ?? Text;
}

internal sealed record XLongText(string? Text);

internal sealed record XAttachments(IReadOnlyList<string>? MediaKeys);

internal sealed record XPublicMetrics(
    long? RetweetCount,
    long? RepostCount,
    long ReplyCount,
    long LikeCount,
    long QuoteCount,
    long? ImpressionCount)
{
    public long Reposts => RetweetCount ?? RepostCount ?? 0;
}

internal sealed record XIncludes(IReadOnlyList<XUser>? Users, IReadOnlyList<XMedia>? Media);

internal sealed record XUser(string Id, string Name, string Username, string? ProfileImageUrl, bool? Verified);

/// <summary>Photos carry <see cref="Url"/>; videos and GIFs carry <see cref="PreviewImageUrl"/>.</summary>
internal sealed record XMedia(string MediaKey, string Type, string? Url, string? PreviewImageUrl, string? AltText)
{
    public string? DisplayUrl => Url ?? PreviewImageUrl;
}

internal sealed record XMeta(string? NewestId, string? OldestId, int ResultCount, string? NextToken);

/// <summary>RFC 7807-style problem returned by X for errors (or partial errors in lookups).</summary>
internal sealed record XProblem(string? Title, string? Detail, string? Type, int? Status, string? ResourceId);

internal static class XJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
