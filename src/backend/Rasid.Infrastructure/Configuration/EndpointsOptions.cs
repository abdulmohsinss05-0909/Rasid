using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Rasid.Infrastructure.Configuration;

/// <summary>
/// Every base URL the system uses, loaded from <c>config/endpoints.{Environment}.json</c>.
/// No URL literal exists anywhere else in the codebase (ADR-008).
/// </summary>
public sealed class EndpointsOptions
{
    public const string SectionName = "Endpoints";

    public BaseUrlEndpoint XApi { get; set; } = new();

    public XWebEndpoint XWeb { get; set; } = new();

    public BaseUrlEndpoint WebApp { get; set; } = new();

    public BaseUrlEndpoint Api { get; set; } = new();

    public CorsEndpoint Cors { get; set; } = new();
}

public sealed class BaseUrlEndpoint
{
    public string BaseUrl { get; set; } = string.Empty;
}

public sealed class XWebEndpoint
{
    /// <summary>Template with <c>{username}</c> and <c>{postId}</c> placeholders.</summary>
    public string PostUrlTemplate { get; set; } = string.Empty;

    public string BuildPostUrl(string username, string postId) =>
        PostUrlTemplate
            .Replace("{username}", Uri.EscapeDataString(username), StringComparison.Ordinal)
            .Replace("{postId}", Uri.EscapeDataString(postId), StringComparison.Ordinal);
}

public sealed class CorsEndpoint
{
    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];
}

/// <summary>
/// Fails fast at startup: all URLs absolute, and HTTPS outside Development.
/// UAT also accepts loopback HTTP URLs (the local Docker stack on http://localhost:8080): they are not
/// reachable from the network. Production and the X API always require HTTPS.
/// </summary>
internal sealed class EndpointsOptionsValidator(IHostEnvironment environment) : IValidateOptions<EndpointsOptions>
{
    public ValidateOptionsResult Validate(string? name, EndpointsOptions options)
    {
        var failures = new List<string>();
        var requireHttps = !environment.IsDevelopment();
        var allowLoopbackHttp = !environment.IsProduction();

        Check(nameof(options.XApi), options.XApi.BaseUrl, requireHttps: true, allowLoopback: false);
        Check(nameof(options.WebApp), options.WebApp.BaseUrl, requireHttps, allowLoopbackHttp);
        Check(nameof(options.Api), options.Api.BaseUrl, requireHttps, allowLoopbackHttp);

        if (!options.XWeb.PostUrlTemplate.Contains("{postId}", StringComparison.Ordinal))
        {
            failures.Add("Endpoints:XWeb:PostUrlTemplate must contain '{postId}'.");
        }

        foreach (var origin in options.Cors.AllowedOrigins)
        {
            Check("Cors origin", origin, requireHttps, allowLoopbackHttp);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);

        void Check(string key, string value, bool requireHttps, bool allowLoopback)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                failures.Add($"Endpoints:{key} must be an absolute URL (was '{value}').");
            }
            else if (requireHttps && uri.Scheme != Uri.UriSchemeHttps && !(allowLoopback && uri.IsLoopback))
            {
                failures.Add($"Endpoints:{key} must use HTTPS in the '{environment.EnvironmentName}' environment.");
            }
        }
    }
}
