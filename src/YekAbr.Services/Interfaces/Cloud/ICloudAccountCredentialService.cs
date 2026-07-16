using YekAbr.Domain.Entities;

namespace YekAbr.Services.Interfaces.Cloud;

public interface ICloudAccountCredentialService
{
    Task<ConnectedCloudAccount?> GetOwnedActiveAccountAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<string> GetValidAccessTokenAsync(
        ConnectedCloudAccount account,
        CancellationToken cancellationToken = default);
}
