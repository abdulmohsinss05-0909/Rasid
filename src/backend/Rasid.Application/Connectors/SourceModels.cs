using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;
using Rasid.Domain.Quota;

namespace Rasid.Application.Connectors;

/// <summary>A discovery request: an X query plus the plain terms (used by connectors that cannot parse X syntax).</summary>
public sealed record SourceSearchRequest(
    string Query,
    IReadOnlyList<string> MatchTerms,
    string? SinceId,
    int MaxResults,
    string? PaginationToken);

public sealed record SourceAuthor(string Id, string Username, string DisplayName, string? ProfileImageUrl, bool IsVerified);

public sealed record SourcePost(
    string Id,
    string AuthorId,
    string Text,
    string? Language,
    DateTimeOffset CreatedAtUtc,
    string? Url,
    EngagementMetrics Metrics,
    PostMedia? Media = null);

/// <summary>One page of search results.</summary>
public sealed record SourcePage(
    IReadOnlyList<SourcePost> Posts,
    IReadOnlyList<SourceAuthor> Authors,
    string? NextToken,
    RateLimitWindow? RateLimit);

/// <summary>Result of looking up known posts. Requested IDs that were not returned are no longer available.</summary>
public sealed record SourceLookup(
    IReadOnlyList<SourcePost> Posts,
    IReadOnlyList<SourceAuthor> Authors,
    IReadOnlyCollection<string> UnavailableIds,
    RateLimitWindow? RateLimit);

public enum SourceFailureKind
{
    Unauthorized = 1,
    RateLimited = 2,
    Unavailable = 3,
    InvalidRequest = 4,
}

public sealed record SourceFailure(SourceFailureKind Kind, string Message, DateTimeOffset? RetryAfterUtc = null);

/// <summary>Outcome of a connector call. Connectors never throw for expected remote failures.</summary>
public sealed class SourceResult<T>
    where T : class
{
    private SourceResult(T? value, SourceFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    public T? Value { get; }

    public SourceFailure? Failure { get; }

    public bool IsSuccess => Failure is null;

#pragma warning disable CA1000 // Static factories are the intended API.
    public static SourceResult<T> Success(T value) => new(value ?? throw new ArgumentNullException(nameof(value)), null);

    public static SourceResult<T> Fail(SourceFailure failure) => new(null, failure ?? throw new ArgumentNullException(nameof(failure)));
#pragma warning restore CA1000
}
