using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

public interface IMegaConnectionService
{
    Task<Result<ConnectedCloudAccountDto>> ConnectAsync(
        string userId,
        ConnectMegaAccountRequest request,
        CancellationToken cancellationToken = default);
}
