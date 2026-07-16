using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

/// <summary>
/// Optional capability for providers that issue short-lived access tokens.
/// </summary>
public interface ICloudTokenRefreshProvider
{
    Task<CloudOAuthTokenResult> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}
