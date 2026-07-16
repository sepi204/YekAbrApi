using Microsoft.AspNetCore.Identity;
using YekAbr.Domain.Entities;

namespace YekAbr.Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
