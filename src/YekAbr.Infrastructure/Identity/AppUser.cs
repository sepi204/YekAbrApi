using Microsoft.AspNetCore.Identity;
using YekAbr.Domain.Entities;

namespace YekAbr.Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    /// <summary>
    /// Relative web path to the profile image (e.g. /uploads/profiles/{file}.jpg). Null when unset.
    /// </summary>
    public string? ProfileImageUrl { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
