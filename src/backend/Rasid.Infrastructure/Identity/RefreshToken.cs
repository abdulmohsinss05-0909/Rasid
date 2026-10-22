namespace Rasid.Infrastructure.Identity;

/// <summary>
/// A refresh token. Only a SHA-256 hash is stored, so a database leak does not expose usable tokens.
/// Tokens rotate on every use; reuse of a revoked token revokes the user's whole token family.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string UserId { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset ExpiresUtc { get; set; }

    public DateTimeOffset? RevokedUtc { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTimeOffset nowUtc) => RevokedUtc is null && ExpiresUtc > nowUtc;
}
