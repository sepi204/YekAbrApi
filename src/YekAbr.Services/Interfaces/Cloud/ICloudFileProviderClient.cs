using YekAbr.Domain.Models;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Interfaces.Cloud;

/// <summary>
/// Provider-neutral file and folder operations.
/// </summary>
public interface ICloudFileProviderClient : ICloudProviderClient
{
    Task<CloudStorageUsage> GetStorageUsageAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<CloudItemListResult> ListItemsAsync(
        string accessToken,
        ListCloudItemsRequest request,
        CancellationToken cancellationToken = default);

    Task<CloudItem> GetItemAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default);

    Task<CloudItem> UploadFileAsync(
        string accessToken,
        UploadCloudFileRequest request,
        CancellationToken cancellationToken = default);

    Task<CloudDownloadResult> DownloadFileAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default);

    Task DeleteItemAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default);

    Task<CloudItem> CreateFolderAsync(
        string accessToken,
        CreateCloudFolderRequest request,
        CancellationToken cancellationToken = default);

    Task<CloudItem> MoveItemAsync(
        string accessToken,
        string itemId,
        string destinationParentFolderId,
        CancellationToken cancellationToken = default);

    Task<CloudItem> RenameItemAsync(
        string accessToken,
        string itemId,
        string newName,
        CancellationToken cancellationToken = default);
}
