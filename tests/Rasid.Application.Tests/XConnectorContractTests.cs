using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rasid.Application.Connectors;
using Rasid.Infrastructure.Configuration;
using Rasid.Infrastructure.Connectors.X;

namespace Rasid.Application.Tests;

/// <summary>
/// Contract tests for the X connector with responses shaped like X API v2 (docs.x.com). They prove the
/// switch to live data is configuration only: both the current and the documented future field names
/// are understood, media is mapped, and rate limits and deleted posts are handled.
/// </summary>
public sealed class XConnectorContractTests
{
    private const string SearchWithLegacyNames = """
        {
          "data": [{
            "id": "1850000000000000002",
            "text": "Short text…",
            "author_id": "42",
            "created_at": "2026-10-20T10:00:00.000Z",
            "lang": "en",
            "public_metrics": { "retweet_count": 7, "reply_count": 3, "like_count": 50, "quote_count": 1, "impression_count": 2000, "bookmark_count": 4 },
            "note_tweet": { "text": "The full long-form text of the post #UAEAid" },
            "attachments": { "media_keys": ["3_1"] }
          }],
          "includes": {
            "users": [{ "id": "42", "name": "Relief Agency", "username": "relief_agency", "profile_image_url": "https://pbs.twimg.com/profile_images/a.jpg", "verified": true }],
            "media": [{ "media_key": "3_1", "type": "photo", "url": "https://pbs.twimg.com/media/convoy.jpg", "alt_text": "Trucks on a road" }]
          },
          "meta": { "newest_id": "1850000000000000002", "oldest_id": "1850000000000000002", "result_count": 1, "next_token": "b26v89c19zqg8o3f" }
        }
        """;

    private const string SearchWithPostNames = """
        {
          "data": [{
            "id": "1850000000000000003",
            "text": "Video post",
            "author_id": "42",
            "created_at": "2026-10-20T11:00:00.000Z",
            "public_metrics": { "repost_count": 9, "reply_count": 0, "like_count": 12, "quote_count": 0 },
            "note_post": { "text": "Full text from note_post" },
            "attachments": { "media_keys": ["7_9"] }
          }],
          "includes": {
            "users": [{ "id": "42", "name": "Relief Agency", "username": "relief_agency" }],
            "media": [{ "media_key": "7_9", "type": "video", "preview_image_url": "https://pbs.twimg.com/ext_tw_video_thumb/v.jpg" }]
          },
          "meta": { "result_count": 1 }
        }
        """;

    [Fact]
    public async Task Maps_posts_authors_metrics_long_text_and_photo_media()
    {
        var (source, handler) = Create(_ => Json(HttpStatusCode.OK, SearchWithLegacyNames));

        var result = await source.SearchRecentAsync(new SourceSearchRequest("#UAEAid", [], "1850000000000000001", 100, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var post = Assert.Single(result.Value!.Posts);
        Assert.Equal("The full long-form text of the post #UAEAid", post.Text);
        Assert.Equal(50, post.Metrics.Likes);
        Assert.Equal(7, post.Metrics.Reposts);
        Assert.Equal(2000, post.Metrics.Views);
        Assert.Equal("https://x.com/relief_agency/status/1850000000000000002", post.Url);
        Assert.Equal("photo", post.Media!.Kind);
        Assert.Equal("https://pbs.twimg.com/media/convoy.jpg", post.Media.PreviewUrl);
        Assert.Equal("Trucks on a road", post.Media.AltText);
        Assert.Equal("b26v89c19zqg8o3f", result.Value.NextToken);
        Assert.True(Assert.Single(result.Value.Authors).IsVerified);

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("tweet.fields=", query, StringComparison.Ordinal);
        Assert.Contains("media.fields=", query, StringComparison.Ordinal);
        Assert.Contains("attachments.media_keys", Uri.UnescapeDataString(query), StringComparison.Ordinal);
        Assert.Contains("since_id=1850000000000000001", query, StringComparison.Ordinal);
        Assert.Equal("Bearer test-token", handler.LastRequest.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Understands_the_post_naming_and_video_previews_with_missing_views()
    {
        var (source, _) = Create(_ => Json(HttpStatusCode.OK, SearchWithPostNames));

        var post = Assert.Single((await source.SearchRecentAsync(new SourceSearchRequest("q", [], null, 10, null), CancellationToken.None)).Value!.Posts);

        Assert.Equal("Full text from note_post", post.Text);
        Assert.Equal(9, post.Metrics.Reposts);
        Assert.Null(post.Metrics.Views);
        Assert.Equal("video", post.Media!.Kind);
        Assert.Equal("https://pbs.twimg.com/ext_tw_video_thumb/v.jpg", post.Media.PreviewUrl);
    }

    [Fact]
    public async Task Field_parameter_name_is_configuration_not_code()
    {
        var (source, handler) = Create(_ => Json(HttpStatusCode.OK, SearchWithPostNames), o => o.PostFieldsParameter = "post.fields");

        await source.SearchRecentAsync(new SourceSearchRequest("q", [], null, 10, null), CancellationToken.None);

        Assert.Contains("post.fields=", handler.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("tweet.fields=", handler.LastRequest.RequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rate_limit_reports_the_reset_time_from_the_headers()
    {
        var reset = DateTimeOffset.UtcNow.AddMinutes(9);
        var (source, _) = Create(_ =>
        {
            var response = Json(HttpStatusCode.TooManyRequests, """{ "title": "Too Many Requests", "detail": "Too Many Requests", "type": "about:blank", "status": 429 }""");
            response.Headers.Add("x-rate-limit-limit", "450");
            response.Headers.Add("x-rate-limit-remaining", "0");
            response.Headers.Add("x-rate-limit-reset", reset.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            return response;
        });

        var result = await source.SearchRecentAsync(new SourceSearchRequest("q", [], null, 10, null), CancellationToken.None);

        Assert.Equal(SourceFailureKind.RateLimited, result.Failure!.Kind);
        Assert.Equal(reset.ToUnixTimeSeconds(), result.Failure.RetryAfterUtc!.Value.ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, SourceFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, SourceFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest, SourceFailureKind.InvalidRequest)]
    public async Task Http_errors_become_typed_failures(HttpStatusCode status, SourceFailureKind expected)
    {
        var (source, _) = Create(_ => Json(status, """{ "title": "Error", "detail": "Explained", "status": 400 }"""));

        var result = await source.SearchRecentAsync(new SourceSearchRequest("q", [], null, 10, null), CancellationToken.None);

        Assert.Equal(expected, result.Failure!.Kind);
    }

    [Fact]
    public async Task Lookup_marks_posts_x_did_not_return_as_unavailable()
    {
        const string lookup = """
            {
              "data": [{ "id": "1850000000000000002", "text": "still here", "author_id": "42", "created_at": "2026-10-20T10:00:00.000Z",
                         "public_metrics": { "retweet_count": 1, "reply_count": 0, "like_count": 2, "quote_count": 0, "impression_count": 30 } }],
              "errors": [{ "resource_id": "1850000000000000009", "title": "Not Found Error", "type": "https://api.x.com/2/problems/resource-not-found", "detail": "Could not find tweet." }]
            }
            """;
        var (source, handler) = Create(_ => Json(HttpStatusCode.OK, lookup));

        var result = await source.LookupAsync(["1850000000000000002", "1850000000000000009"], CancellationToken.None);

        Assert.Single(result.Value!.Posts);
        Assert.Equal(["1850000000000000009"], result.Value.UnavailableIds);
        Assert.Contains("ids=1850000000000000002,1850000000000000009", Uri.UnescapeDataString(handler.LastRequest!.RequestUri!.Query), StringComparison.Ordinal);
    }

    private static (XPostSource Source, StubHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond, Action<XApiOptions>? configure = null)
    {
        var settings = new XApiOptions { BearerToken = "test-token" };
        configure?.Invoke(settings);
        var monitor = new StaticOptionsMonitor<XApiOptions>(settings);

        var stub = new StubHandler(respond);
        var pipeline = new XRateLimitHandler(NullLogger<XRateLimitHandler>.Instance)
        {
            InnerHandler = new XAuthHandler(monitor) { InnerHandler = stub },
        };
        var http = new HttpClient(pipeline) { BaseAddress = new Uri("https://api.x.com/2/") };

        var endpoints = Options.Create(new EndpointsOptions
        {
            XWeb = new XWebEndpoint { PostUrlTemplate = "https://x.com/{username}/status/{postId}" },
        });

        return (new XPostSource(new XApiClient(http, monitor, TimeProvider.System), endpoints, Options.Create(settings)), stub);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
