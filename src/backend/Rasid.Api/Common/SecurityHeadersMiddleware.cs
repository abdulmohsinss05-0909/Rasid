namespace Rasid.Api.Common;

/// <summary>Hardened response headers for a JSON API (docs/08-security.md, OWASP A05).</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // The interactive API reference (Development/UAT only) needs to run its own scripts.
    private static readonly PathString[] ApiReferencePaths = ["/swagger", "/scalar", "/openapi"];

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            if (!ApiReferencePaths.Any(p => context.Request.Path.StartsWithSegments(p)))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
