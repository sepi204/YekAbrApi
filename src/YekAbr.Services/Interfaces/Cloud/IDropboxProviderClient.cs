using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

/// <summary>
/// Dropbox provider: OAuth + file operations.
/// </summary>
public interface IDropboxProviderClient : ICloudFileProviderClient, ICloudTokenRefreshProvider
{
    string BuildAuthorizationUrl(string state);

    Task<CloudOAuthTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<CloudProviderAccountInfo> GetAccountInfoAsync(string accessToken, CancellationToken cancellationToken = default);
}
