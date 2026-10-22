using Microsoft.Extensions.Options;
using Rasid.Application.Connectors;
using Rasid.Domain.Common;
using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;
using Rasid.Infrastructure.Configuration;

namespace Rasid.Infrastructure.Connectors.X;

/// <summary>Adapter: X API v2 → the application's <see cref="IPostSource"/> port.</summary>
internal sealed class XPostSource(XApiClient client, IOptions<EndpointsOptions> endpoints, IOptions<XApiOptions> options) : IPostSource
{
    // X serves https://x.com/i/status/{id} for any post when the author is unknown.
    private const string UnknownUsernamePathSegment = "i";

    public DataSource Source => DataSource.XApi;

    public int MaxLookupBatchSize => XApiClient.MaxLookupIds;

    public TimeSpan? MaxCursorAge => TimeSpan.FromDays(options.Value.CursorMaxAgeDays);

    /// <summary>X Recent Search covers the last 7 days.</summary>
    public TimeSpan SearchWindow => TimeSpan.FromDays(7);

    public async Task<SourceResult<SourcePage>> SearchRecentAsync(SourceSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await client.SearchRecentAsync(
            request.Query, request.SinceId, request.MaxResults, request.PaginationToken, cancellationToken);

        if (result.Failure is not null)
        {
            return SourceResult<SourcePage>.Fail(result.Failure);
        }

        var (posts, authors) = Map(result.Body!);
        return SourceResult<SourcePage>.Success(new SourcePage(posts, authors, result.Body!.Meta?.NextToken, result.Window));
    }

    public async Task<SourceResult<SourceLookup>> LookupAsync(IReadOnlyCollection<string> postIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(postIds);

        var result = await client.LookupAsync(postIds, cancellationToken);
        if (result.Failure is not null)
        {
            return SourceResult<SourceLookup>.Fail(result.Failure);
        }

        var (posts, authors) = Map(result.Body!);

        // Any requested ID that X did not return is deleted, protected or otherwise unavailable.
        var returned = posts.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var unavailable = postIds.Where(id => !returned.Contains(id)).ToList();

        return SourceResult<SourceLookup>.Success(new SourceLookup(posts, authors, unavailable, result.Window));
    }

    private (List<SourcePost> Posts, List<SourceAuthor> Authors) Map(XTweetsResponse response)
    {
        var authors = (response.Includes?.Users ?? [])
            .Select(u => new SourceAuthor(u.Id, u.Username, u.Name, u.ProfileImageUrl, u.Verified ?? false))
            .ToList();
        var usernames = authors.DistinctBy(a => a.Id).ToDictionary(a => a.Id, a => a.Username, StringComparer.Ordinal);
        var media = (response.Includes?.Media ?? []).DistinctBy(m => m.MediaKey).ToDictionary(m => m.MediaKey, StringComparer.Ordinal);

        var posts = (response.Data ?? [])
            .Select(t => new SourcePost(
                t.Id,
                t.AuthorId,
                // Display requirement: show the full, unaltered text (long posts carry it separately).
                t.FullText,
                t.Lang,
                t.CreatedAt.ToUniversalTime(),
                endpoints.Value.XWeb.BuildPostUrl(usernames.GetValueOrDefault(t.AuthorId) ?? UnknownUsernamePathSegment, t.Id),
                ToMetrics(t.PublicMetrics),
                FirstMedia(t, media)))
            .ToList();

        return (posts, authors);
    }

    private static PostMedia? FirstMedia(XTweet tweet, Dictionary<string, XMedia> media)
    {
        var key = tweet.Attachments?.MediaKeys?.FirstOrDefault(media.ContainsKey);
        return key is null ? null : PostMedia.Create(media[key].Type, media[key].DisplayUrl, media[key].AltText);
    }

    private static EngagementMetrics ToMetrics(XPublicMetrics? metrics) => metrics is null
        ? EngagementMetrics.Empty
        : new EngagementMetrics(
            metrics.LikeCount,
            metrics.Reposts,
            metrics.ReplyCount,
            metrics.QuoteCount,
            metrics.ImpressionCount);
}
