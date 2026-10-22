using System.Security.Claims;
using Rasid.Api.Common;
using Rasid.Application.Auth;
using Rasid.Infrastructure.Identity;

namespace Rasid.Api.Endpoints;

internal sealed class AuthEndpoints : IEndpointGroup
{
    /// <summary>HttpOnly cookie that carries the refresh token; scoped to the auth routes only.</summary>
    private const string RefreshCookie = "rasid_refresh";
    private const string RefreshCookiePath = EndpointGroupExtensions.ApiPrefix + "/auth";

    public void Map(IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Authentication").RequireRateLimiting(RateLimitPolicies.Auth);

        auth.MapPost("/login", SignInAsync).AllowAnonymous().WithSummary("Sign in with email and password.");
        auth.MapPost("/refresh", RefreshAsync).AllowAnonymous().WithSummary("Exchange the refresh cookie for a new access token.");
        auth.MapPost("/logout", SignOutAsync).AllowAnonymous().WithSummary("Revoke the current refresh token.");
        auth.MapGet("/me", GetCurrentUserAsync).RequireAuthorization().WithSummary("The signed-in user.");
    }

    private static async Task<IResult> SignInAsync(SignInRequest request, IAuthService auth, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Email and password are required."],
            });
        }

        var result = await auth.SignInAsync(request.Email.Trim(), request.Password, ct);
        return result.ToHttpResult(session => Issue(http, session));
    }

    private static async Task<IResult> RefreshAsync(IAuthService auth, HttpContext http, CancellationToken ct)
    {
        var token = http.Request.Cookies[RefreshCookie] ?? string.Empty;
        var result = await auth.RefreshAsync(token, ct);
        if (!result.IsSuccess)
        {
            ClearCookie(http);
        }

        return result.ToHttpResult(session => Issue(http, session));
    }

    private static async Task<IResult> SignOutAsync(IAuthService auth, HttpContext http, CancellationToken ct)
    {
        await auth.SignOutAsync(http.Request.Cookies[RefreshCookie] ?? string.Empty, ct);
        ClearCookie(http);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(ClaimsPrincipal user, IAuthService auth, CancellationToken ct)
    {
        var userId = user.FindFirst(TokenService.SubjectClaim)?.Value;
        return userId is null
            ? TypedResults.Unauthorized()
            : (await auth.GetUserAsync(userId, ct)).ToHttpResult();
    }

    /// <summary>The access token goes in the body (kept in memory by the SPA); the refresh token only in an HttpOnly cookie.</summary>
    private static IResult Issue(HttpContext http, AuthSession session)
    {
        http.Response.Cookies.Append(RefreshCookie, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = session.RefreshTokenExpiresUtc,
            IsEssential = true,
        });

        return TypedResults.Ok(new SignInResponse(session.AccessToken, session.AccessTokenExpiresUtc, session.User));
    }

    private static void ClearCookie(HttpContext http) =>
        http.Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = RefreshCookiePath, Secure = true, SameSite = SameSiteMode.Strict });

    internal sealed record SignInRequest(string Email, string Password);

    internal sealed record SignInResponse(string AccessToken, DateTimeOffset ExpiresUtc, UserDto User);
}
