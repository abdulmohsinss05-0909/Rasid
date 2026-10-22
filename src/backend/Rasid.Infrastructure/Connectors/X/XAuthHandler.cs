using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Rasid.Infrastructure.Connectors.X;

/// <summary>Attaches the App-Only bearer token. The token never leaves this handler (not logged, not returned).</summary>
internal sealed class XAuthHandler(IOptionsMonitor<XApiOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = options.CurrentValue.BearerToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("X API bearer token is not configured.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}
