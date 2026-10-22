using Microsoft.AspNetCore.Identity;

namespace Rasid.Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    public const int DisplayNameMaxLength = 100;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }
}
