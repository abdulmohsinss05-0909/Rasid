using Rasid.Application.Common.Results;

namespace Rasid.Application.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Analyst = "Analyst";
    public const string Viewer = "Viewer";

    public static IReadOnlyList<string> All { get; } = [Admin, Analyst, Viewer];
}

/// <summary>Authorisation policies, enforced server-side on every endpoint.</summary>
public static class Policies
{
    /// <summary>Viewer, Analyst or Admin: read dashboards.</summary>
    public const string CanView = "CanView";

    /// <summary>Analyst or Admin: manage topics, trigger refresh.</summary>
    public const string CanManageTopics = "CanManageTopics";

    /// <summary>Admin only: system status and operations.</summary>
    public const string CanAdminister = "CanAdminister";
}

public sealed record UserDto(string Id, string Email, string DisplayName, IReadOnlyList<string> Roles);

/// <summary>
/// Result of a successful sign-in or refresh. The refresh token is returned to the API layer only,
/// which places it in an HttpOnly cookie — it is never exposed to browser JavaScript.
/// </summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresUtc,
    UserDto User);

public interface IAuthService
{
    Task<Result<AuthSession>> SignInAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Exchanges a valid refresh token for a new session and revokes the old token (rotation).</summary>
    Task<Result<AuthSession>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task SignOutAsync(string refreshToken, CancellationToken cancellationToken);

    Task<Result<UserDto>> GetUserAsync(string userId, CancellationToken cancellationToken);
}
