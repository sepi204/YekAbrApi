using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Auth;

namespace YekAbr.Services.Interfaces.Auth;

public interface IAuthService
{
    Task<Result<AuthTokensDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthTokensDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthTokensDto>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<Result<object>> LogoutAsync(string userId, LogoutRequest request, CancellationToken cancellationToken = default);
    Task<Result<object>> LogoutAllAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
}
