using System.Globalization;
using Rasid.Application.Connectors;
using Rasid.Domain.Common;
using Rasid.Domain.Posts;

namespace Rasid.Infrastructure.Connectors.Sample;

/// <summary>
/// Clearly labelled demonstration connector (<see cref="DataSource.Sample"/>). Implements the same port
/// as the X connector so the full pipeline runs unchanged without X credentials. It understands plain
/// match terms only (not full X query syntax) and never produces links to real posts.
/// </summary>
internal sealed class SamplePostSource(TimeProvider clock) : IPostSource
{
    private const string TokenPrefix = "offset:";

    /// <summary>Sample images are served by the web app and labelled "Sample image" in the UI.</summary>
    private const string MediaPathPrefix = "/sample-media/";

    public DataSource Source => DataSource.Sample;

    public int MaxLookupBatchSize => 100;

    public TimeSpan? MaxCursorAge => null;

    public TimeSpan SearchWindow => TimeSpan.FromDays(SampleDataGenerator.WindowDays);

    public Task<SourceResult<SourcePage>> SearchRecentAsync(SourceSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = clock.GetUtcNow();
        var matches = SampleDataGenerator
            .PostsBetween(now.AddDays(-SampleDataGenerator.WindowDays), now)
            .Where(p => !SampleDataGenerator.IsDeletedAt(p, now))
            .Where(p => request.SinceId is null || PostId.IsNewer(p.Id, request.SinceId))
            .Where(p => Matches(p, request.MatchTerms))
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();

        var offset = ParseOffset(request.PaginationToken);
        var page = matches.Skip(offset).Take(request.MaxResults).ToList();
        var nextOffset = offset + page.Count;
        var nextToken = nextOffset < matches.Count
            ? string.Create(CultureInfo.InvariantCulture, $"{TokenPrefix}{nextOffset}")
            : null;

        var result = new SourcePage(
            [.. page.Select(p => ToSourcePost(p, now))],
            [.. page.Select(SampleDataGenerator.AuthorFor).DistinctBy(a => a.Id)],
            nextToken,
            RateLimit: null);

        return Task.FromResult(SourceResult<SourcePage>.Success(result));
    }

    public Task<SourceResult<SourceLookup>> LookupAsync(IReadOnlyCollection<string> postIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(postIds);

        var now = clock.GetUtcNow();
        var found = postIds
            .Select(SampleDataGenerator.FindById)
            .OfType<GeneratedPost>()
            .Where(p => !SampleDataGenerator.IsDeletedAt(p, now))
            .ToList();

        var foundIds = found.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var result = new SourceLookup(
            [.. found.Select(p => ToSourcePost(p, now))],
            [.. found.Select(SampleDataGenerator.AuthorFor).DistinctBy(a => a.Id)],
            [.. postIds.Where(id => !foundIds.Contains(id))],
            RateLimit: null);

        return Task.FromResult(SourceResult<SourceLookup>.Success(result));
    }

    private static bool Matches(GeneratedPost post, IReadOnlyList<string> terms)
    {
        var username = "@" + SampleCatalog.Authors[post.AuthorIndex].Username;
        return terms.Any(term =>
            post.Text.Contains(term, StringComparison.OrdinalIgnoreCase)
            || string.Equals(username, term, StringComparison.OrdinalIgnoreCase));
    }

    private static SourcePost ToSourcePost(GeneratedPost post, DateTimeOffset now) => new(
        post.Id,
        SampleDataGenerator.AuthorFor(post).Id,
        post.Text,
        post.Language,
        post.CreatedAtUtc,
        Url: null,
        SampleDataGenerator.MetricsAt(post, now),
        post.MediaFile is null ? null : PostMedia.Create("photo", MediaPathPrefix + post.MediaFile, post.MediaAlt));

    private static int ParseOffset(string? token) =>
        token is not null
        && token.StartsWith(TokenPrefix, StringComparison.Ordinal)
        && int.TryParse(token.AsSpan(TokenPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
            ? offset
            : 0;
}
