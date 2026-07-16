using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

public interface IGoogleDriveConnectionService
{
    Task<Result<GoogleConnectUrlDto>> GetConnectUrlAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result<ConnectedCloudAccountDto>> HandleCallbackAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default);
}
