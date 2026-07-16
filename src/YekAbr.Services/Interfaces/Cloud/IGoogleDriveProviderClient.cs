using YekAbr.Domain.Models;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

/// <summary>
/// Google Drive provider: OAuth + file operations.
/// </summary>
public interface IGoogleDriveProviderClient : ICloudFileProviderClient
{
    string BuildAuthorizationUrl(string state);

    Task<CloudOAuthTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<CloudOAuthTokenResult> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<CloudProviderAccountInfo> GetAccountInfoAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<CloudStorageUsage> GetStorageUsageAsync(string accessToken, CancellationToken cancellationToken = default);
}
