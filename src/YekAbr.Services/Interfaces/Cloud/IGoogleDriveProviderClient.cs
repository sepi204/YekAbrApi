using YekAbr.Domain.Models;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

/// <summary>
/// Google Drive-specific operations required for account connection and usage.
/// </summary>
public interface IGoogleDriveProviderClient : ICloudProviderClient
{
    string BuildAuthorizationUrl(string state);

    Task<CloudOAuthTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<CloudOAuthTokenResult> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<CloudProviderAccountInfo> GetAccountInfoAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<CloudStorageUsage> GetStorageUsageAsync(string accessToken, CancellationToken cancellationToken = default);
}
