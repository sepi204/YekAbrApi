using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

public interface ICloudAccountService
{
    Task<Result<IReadOnlyList<ConnectedCloudAccountDto>>> ListAccountsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<Result<object>> DisconnectAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<Result<CloudStorageUsageDto>> GetUsageAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default);
}
