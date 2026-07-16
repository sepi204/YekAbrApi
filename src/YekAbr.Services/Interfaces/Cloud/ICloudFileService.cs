using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

public interface ICloudFileService
{
    Task<Result<CloudItemListDto>> ListItemsAsync(
        string userId,
        Guid accountId,
        ListCloudItemsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CloudItemDto>> GetItemAsync(
        string userId,
        Guid accountId,
        string itemId,
        CancellationToken cancellationToken = default);

    Task<Result<CloudItemDto>> UploadFileAsync(
        string userId,
        Guid accountId,
        UploadCloudFileRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CloudDownloadResult>> DownloadFileAsync(
        string userId,
        Guid accountId,
        string itemId,
        CancellationToken cancellationToken = default);

    Task<Result<object>> DeleteItemAsync(
        string userId,
        Guid accountId,
        string itemId,
        CancellationToken cancellationToken = default);

    Task<Result<CloudItemDto>> CreateFolderAsync(
        string userId,
        Guid accountId,
        CreateCloudFolderRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CloudItemDto>> MoveItemAsync(
        string userId,
        Guid accountId,
        string itemId,
        MoveCloudItemRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CloudItemDto>> RenameItemAsync(
        string userId,
        Guid accountId,
        string itemId,
        RenameCloudItemRequest request,
        CancellationToken cancellationToken = default);
}
