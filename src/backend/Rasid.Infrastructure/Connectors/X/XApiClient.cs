using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Rasid.Application.Connectors;
using Rasid.Domain.Quota;

namespace Rasid.Infrastructure.Connectors.X;

/// <summary>
/// Typed HTTP client for the two X API v2 endpoints Rasid uses. Converts every remote failure into a
/// <see cref="SourceFailure"/> — callers never see transport exceptions.
/// Pipeline (outer → inner): XRateLimitHandler → XAuthHandler → resilience (timeout, retry, circuit breaker).
/// </summary>
internal sealed class XApiClient(HttpClient httpClient, IOptionsMonitor<XApiOptions> options, TimeProvider clock)
{
    public const int MinPageSize = 10;
    public const int MaxPageSize = 100;
    public const int MaxLookupIds = 100;

    private static readonly TimeSpan DefaultRateLimitBackoff = TimeSpan.FromMinutes(15);

    public Task<XCallResult> SearchRecentAsync(
        string query, string? sinceId, int maxResults, string? nextToken, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["query"] = query,
            ["max_results"] = Math.Clamp(maxResults, MinPageSize, MaxPageSize).ToString(CultureInfo.InvariantCulture),
        };
        AddFieldParameters(parameters);

        if (!string.IsNullOrEmpty(sinceId))
        {
            parameters["since_id"] = sinceId;
        }

        if (!string.IsNullOrEmpty(nextToken))
        {
            parameters["next_token"] = nextToken;
        }

        return SendAsync(QueryHelpers.AddQueryString("tweets/search/recent", parameters), cancellationToken);
    }

    public Task<XCallResult> LookupAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count is 0 or > MaxLookupIds)
        {
            throw new ArgumentOutOfRangeException(nameof(ids), $"Between 1 and {MaxLookupIds} IDs are required.");
        }

        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ids"] = string.Join(',', ids),
        };
        AddFieldParameters(parameters);

        return SendAsync(QueryHelpers.AddQueryString("tweets", parameters), cancellationToken);
    }

    /// <summary>Field and expansion parameters, all from configuration (no code change if X renames them).</summary>
    private void AddFieldParameters(Dictionary<string, string?> parameters)
    {
        var settings = options.CurrentValue;
        parameters[settings.PostFieldsParameter] = settings.PostFields;
        parameters["expansions"] = settings.Expansions;
        parameters["user.fields"] = settings.UserFields;
        parameters["media.fields"] = settings.MediaFields;
    }

    private async Task<XCallResult> SendAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            request.Options.TryGetValue(XRateLimitHandler.WindowKey, out var window);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<XTweetsResponse>(XJson.Options, cancellationToken);
                return XCallResult.Success(body ?? new XTweetsResponse(null, null, null, null), window);
            }

            var problem = await TryReadProblemAsync(response, cancellationToken);
            return XCallResult.Fail(MapFailure(response.StatusCode, problem, window));
        }
        catch (BrokenCircuitException)
        {
            return Unavailable("X API calls are temporarily paused after repeated failures.");
        }
        catch (TimeoutRejectedException)
        {
            return Unavailable("X API did not respond in time.");
        }
        catch (HttpRequestException)
        {
            return Unavailable("X API could not be reached.");
        }
        catch (JsonException)
        {
            return Unavailable("X API returned a response in an unexpected format.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable("X API did not respond in time.");
        }
    }

    private SourceFailure MapFailure(HttpStatusCode status, XProblem? problem, RateLimitWindow? window)
    {
        var detail = problem?.Detail ?? problem?.Title;
        var code = (int)status;

        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new SourceFailure(
                SourceFailureKind.Unauthorized,
                $"X API refused access (HTTP {code}). Check the credentials and that the plan includes this endpoint. {detail}".Trim()),
            HttpStatusCode.TooManyRequests => new SourceFailure(
                SourceFailureKind.RateLimited,
                "X API rate limit reached.",
                window?.ResetUtc ?? clock.GetUtcNow() + DefaultRateLimitBackoff),
            HttpStatusCode.BadRequest => new SourceFailure(
                SourceFailureKind.InvalidRequest,
                $"X API rejected the request (HTTP 400). {detail}".Trim()),
            _ => new SourceFailure(SourceFailureKind.Unavailable, $"X API returned HTTP {code}."),
        };
    }

    private static async Task<XProblem?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<XProblem>(XJson.Options, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null; // Non-JSON error body.
        }
    }

    private static XCallResult Unavailable(string message) =>
        XCallResult.Fail(new SourceFailure(SourceFailureKind.Unavailable, message));
}

internal sealed record XCallResult(XTweetsResponse? Body, RateLimitWindow? Window, SourceFailure? Failure)
{
    public static XCallResult Success(XTweetsResponse body, RateLimitWindow? window) => new(body, window, null);

    public static XCallResult Fail(SourceFailure failure) => new(null, null, failure);
}
