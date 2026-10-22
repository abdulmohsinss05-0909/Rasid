using Rasid.Application.Connectors;
using Rasid.Domain.Common;
using Rasid.Domain.Engagement;
using Rasid.Domain.Posts;
using Rasid.Domain.Quota;

namespace Rasid.Application.Tests.Infrastructure;

/// <summary>A controllable data source: tests set the posts it returns, failures and the source type.</summary>
public sealed class FakePostSource : IPostSource
{
    private readonly List<SourcePost> _posts = [];
    private readonly HashSet<string> _deleted = new(StringComparer.Ordinal);

    public DataSource Source { get; set; } = DataSource.XApi;

    public int MaxLookupBatchSize => 100;

    public TimeSpan? MaxCursorAge => null;

    public TimeSpan SearchWindow { get; set; } = TimeSpan.FromDays(7);

    public SourceFailure? NextFailure { get; set; }

    public int SearchCalls { get; private set; }

    public static SourceAuthor Author { get; } = new("100", "test_account", "Test Account", null, false);

    public void Reset()
    {
        _posts.Clear();
        _deleted.Clear();
        NextFailure = null;
        SearchCalls = 0;
        Source = DataSource.XApi;
        SearchWindow = TimeSpan.FromDays(7);
    }

    public SourcePost AddPost(
        string id, DateTimeOffset createdAt, EngagementMetrics metrics, string text = "UAE aid convoy #UAEAid", string language = "en", PostMedia? media = null)
    {
        var post = new SourcePost(id, Author.Id, text, language, createdAt, $"https://x.com/test_account/status/{id}", metrics, media);
        _posts.Add(post);
        return post;
    }

    public void Delete(string id) => _deleted.Add(id);

    public void UpdateMetrics(string id, EngagementMetrics metrics)
    {
        var index = _posts.FindIndex(p => p.Id == id);
        _posts[index] = _posts[index] with { Metrics = metrics };
    }

    public Task<SourceResult<SourcePage>> SearchRecentAsync(SourceSearchRequest request, CancellationToken cancellationToken)
    {
        SearchCalls++;
        if (TakeFailure() is { } failure)
        {
            return Task.FromResult(SourceResult<SourcePage>.Fail(failure));
        }

        var page = _posts
            .Where(p => !_deleted.Contains(p.Id) && (request.SinceId is null || PostId.IsNewer(p.Id, request.SinceId)))
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();

        var window = new RateLimitWindow(450, 449, DateTimeOffset.UtcNow.AddMinutes(15));
        return Task.FromResult(SourceResult<SourcePage>.Success(new SourcePage(page, [Author], null, window)));
    }

    public Task<SourceResult<SourceLookup>> LookupAsync(IReadOnlyCollection<string> postIds, CancellationToken cancellationToken)
    {
        if (TakeFailure() is { } failure)
        {
            return Task.FromResult(SourceResult<SourceLookup>.Fail(failure));
        }

        var found = _posts.Where(p => postIds.Contains(p.Id) && !_deleted.Contains(p.Id)).ToList();
        var missing = postIds.Except(found.Select(p => p.Id)).ToList();
        return Task.FromResult(SourceResult<SourceLookup>.Success(new SourceLookup(found, [Author], missing, null)));
    }

    private SourceFailure? TakeFailure()
    {
        var failure = NextFailure;
        NextFailure = null;
        return failure;
    }
}
