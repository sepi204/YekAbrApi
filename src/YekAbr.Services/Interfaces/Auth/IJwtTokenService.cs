namespace YekAbr.Services.Interfaces.Auth;

public interface IJwtTokenService
{
    Task<(string Token, DateTime ExpiresAtUtc)> GenerateAccessTokenAsync(string userId, string username, string email, IReadOnlyCollection<string> roles);
    string GenerateRefreshToken();
}
