using System.ComponentModel.DataAnnotations;

namespace Rasid.Infrastructure.Identity;

/// <summary>JWT settings. <see cref="SigningKey"/> comes only from the secret store.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "rasid";

    [Required]
    public string Audience { get; set; } = "rasid-web";

    /// <summary>HMAC-SHA256 key: at least 32 bytes of randomness.</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 30)]
    public int RefreshTokenDays { get; set; } = 7;
}
