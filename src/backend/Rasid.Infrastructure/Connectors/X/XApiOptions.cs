using System.ComponentModel.DataAnnotations;

namespace Rasid.Infrastructure.Connectors.X;

/// <summary>
/// X API v2 settings. The base URL lives in the endpoints file; the bearer token comes only from the
/// secret store (user-secrets locally, environment variables / vault elsewhere).
/// Switching from sample to live data is configuration only: set <see cref="BearerToken"/>.
/// The request field names are configurable too, so a future X naming change (X's docs already
/// describe "post.fields" alongside the "tweet.fields" the API accepts) needs no code change.
/// </summary>
public sealed class XApiOptions
{
    public const string SectionName = "XApi";

    /// <summary>OAuth 2.0 App-Only bearer token. Empty = X connector disabled.</summary>
    public string? BearerToken { get; set; }

    [Range(5, 120)]
    public int AttemptTimeoutSeconds { get; set; } = 20;

    [Range(10, 300)]
    public int TotalTimeoutSeconds { get; set; } = 60;

    [Range(0, 5)]
    public int MaxRetries { get; set; } = 2;

    /// <summary>Query parameter that carries the Post fields.</summary>
    [Required]
    public string PostFieldsParameter { get; set; } = "tweet.fields";

    /// <summary>
    /// Post fields requested. Both long-form text fields are listed so the full text is available
    /// whichever name X returns; unknown names are rejected by X, so keep this list to valid fields.
    /// </summary>
    [Required]
    public string PostFields { get; set; } = "created_at,public_metrics,author_id,lang,attachments,note_tweet";

    [Required]
    public string Expansions { get; set; } = "author_id,attachments.media_keys";

    [Required]
    public string UserFields { get; set; } = "name,username,profile_image_url,verified";

    [Required]
    public string MediaFields { get; set; } = "type,url,preview_image_url,alt_text";

    /// <summary>
    /// X rejects a <c>since_id</c> older than the Recent Search window (7 days). An incremental cursor
    /// older than this is dropped and the run falls back to the full recent window.
    /// </summary>
    [Range(1, 7)]
    public int CursorMaxAgeDays { get; set; } = 6;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BearerToken);
}
