using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Rasid.Domain.Quota;

namespace Rasid.Infrastructure.Connectors.X;

/// <summary>
/// Reads X's <c>x-rate-limit-*</c> headers from the final response (after resilience) and attaches the
/// parsed window to the request, where <see cref="XApiClient"/> picks it up for the Quota Manager.
/// </summary>
internal sealed partial class XRateLimitHandler(ILogger<XRateLimitHandler> logger) : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<RateLimitWindow> WindowKey = new("rasid.x.rate-limit");

    private const int LowRemainingWarning = 5;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (TryParse(response.Headers, out var window))
        {
            request.Options.Set(WindowKey, window);
            if (window.Remaining <= LowRemainingWarning)
            {
                LogLowRemaining(request.RequestUri?.AbsolutePath, window.Remaining, window.ResetUtc);
            }
        }

        return response;
    }

    public static bool TryParse(HttpResponseHeaders headers, out RateLimitWindow window)
    {
        window = null!;

        if (!TryGetLong(headers, "x-rate-limit-limit", out var limit)
            || !TryGetLong(headers, "x-rate-limit-remaining", out var remaining)
            || !TryGetLong(headers, "x-rate-limit-reset", out var resetEpochSeconds))
        {
            return false;
        }

        window = new RateLimitWindow((int)limit, (int)remaining, DateTimeOffset.FromUnixTimeSeconds(resetEpochSeconds));
        return true;
    }

    private static bool TryGetLong(HttpResponseHeaders headers, string name, out long value)
    {
        value = 0;
        return headers.TryGetValues(name, out var values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "X rate-limit window almost exhausted for {Path}: {Remaining} left, resets at {ResetUtc}")]
    private partial void LogLowRemaining(string? path, int remaining, DateTimeOffset resetUtc);
}
