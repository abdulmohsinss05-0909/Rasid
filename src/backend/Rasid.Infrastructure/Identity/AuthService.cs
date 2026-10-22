using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rasid.Application.Auth;
using Rasid.Application.Common.Results;
using Rasid.Infrastructure.Persistence;

namespace Rasid.Infrastructure.Identity;

internal sealed partial class AuthService(
    UserManager<AppUser> userManager,
    RasidDbContext db,
    TokenService tokenService,
    IOptions<JwtOptions> options,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    // One message for every credential failure, so responses never reveal whether an account exists.
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("invalid_credentials", "Invalid email or password.");

    private static readonly Error InvalidSession =
        Error.Unauthorized("invalid_session", "Your session has expired. Please sign in again.");

    public async Task<Result<AuthSession>> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return InvalidCredentials;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            LogLockedOut(user.Id);
            return Error.Unauthorized("account_locked", "Too many failed attempts. Try again later.");
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            LogFailedSignIn(user.Id);
            return InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        LogSignedIn(user.Id);
        return await IssueSessionAsync(user, cancellationToken);
    }

    public async Task<Result<AuthSession>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return InvalidSession;
        }

        var now = clock.GetUtcNow();
        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null)
        {
            return InvalidSession;
        }

        if (stored.RevokedUtc is not null)
        {
            // A revoked token was presented again: likely theft. Revoke every active token of this user.
            await RevokeAllAsync(stored.UserId, now, cancellationToken);
            LogTokenReuse(stored.UserId);
            return InvalidSession;
        }

        if (!stored.IsActive(now))
        {
            return InvalidSession;
        }

        var user = await userManager.FindByIdAsync(stored.UserId);
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return InvalidSession;
        }

        return await IssueSessionAsync(user, cancellationToken, rotating: stored);
    }

    public async Task SignOutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is { RevokedUtc: null })
        {
            stored.RevokedUtc = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<Result<UserDto>> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Error.NotFound("user_not_found", "User not found.");
        }

        return ToDto(user, await userManager.GetRolesAsync(user));
    }

    private async Task<Result<AuthSession>> IssueSessionAsync(AppUser user, CancellationToken cancellationToken, RefreshToken? rotating = null)
    {
        var now = clock.GetUtcNow();
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpires) = tokenService.CreateAccessToken(user, roles);
        var (refreshToken, refreshHash) = TokenService.CreateRefreshToken();
        var refreshExpires = now.AddDays(options.Value.RefreshTokenDays);

        if (rotating is not null)
        {
            rotating.RevokedUtc = now;
            rotating.ReplacedByTokenHash = refreshHash;
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedUtc = now,
            ExpiresUtc = refreshExpires,
        });
        await db.SaveChangesAsync(cancellationToken);

        return new AuthSession(accessToken, accessExpires, refreshToken, refreshExpires, ToDto(user, roles));
    }

    private Task RevokeAllAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedUtc, now), cancellationToken);

    private static UserDto ToDto(AppUser user, IList<string> roles) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName, [.. roles]);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} signed in")]
    private partial void LogSignedIn(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed sign-in for user {UserId}")]
    private partial void LogFailedSignIn(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in refused: user {UserId} is locked out")]
    private partial void LogLockedOut(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh-token reuse detected for user {UserId}; all sessions revoked")]
    private partial void LogTokenReuse(string userId);
}
