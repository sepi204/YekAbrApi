using FluentValidation;
using Microsoft.Extensions.Logging;
using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Cloud;
using YekAbr.Services.Services.Auth;

namespace YekAbr.Infrastructure.Services.Cloud;

public sealed class CloudFileService : ICloudFileService
{
    private readonly ICloudAccountCredentialService _credentialService;
    private readonly ICloudProviderClientFactory _providerFactory;
    private readonly IValidator<ListCloudItemsRequest> _listValidator;
    private readonly IValidator<CreateCloudFolderRequest> _createFolderValidator;
    private readonly IValidator<MoveCloudItemRequest> _moveValidator;
    private readonly IValidator<RenameCloudItemRequest> _renameValidator;
    private readonly ILogger<CloudFileService> _logger;

    public CloudFileService(
        ICloudAccountCredentialService credentialService,
        ICloudProviderClientFactory providerFactory,
        IValidator<ListCloudItemsRequest> listValidator,
        IValidator<CreateCloudFolderRequest> createFolderValidator,
        IValidator<MoveCloudItemRequest> moveValidator,
        IValidator<RenameCloudItemRequest> renameValidator,
        ILogger<CloudFileService> logger)
    {
        _credentialService = credentialService;
        _providerFactory = providerFactory;
        _listValidator = listValidator;
        _createFolderValidator = createFolderValidator;
        _moveValidator = moveValidator;
        _renameValidator = renameValidator;
        _logger = logger;
    }

    public async Task<Result<CloudItemListDto>> ListItemsAsync(
        string userId,
        Guid accountId,
        ListCloudItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await ValidationHelper.ValidateAsync(_listValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<CloudItemListDto>.Failed("اعتبارسنجی ناموفق بود.", validationErrors);
        }

        var access = await ResolveAccessAsync(userId, accountId, cancellationToken);
        if (!access.Success)
        {
            return Result<CloudItemListDto>.Failed(access.ErrorMessage!);
        }

        try
        {
            request.ParentId = NormalizeParentId(request.ParentId, access.Account!.RootFolderId);
            var list = await access.FileProvider!.ListItemsAsync(access.AccessToken!, request, cancellationToken);

            return Result<CloudItemListDto>.Succeeded(
                new CloudItemListDto
                {
                    Items = list.Items.Select(CloudItemDto.FromDomain).ToList(),
                    NextPageToken = list.NextPageToken
                },
                "لیست فایل‌ها و پوشه‌ها با موفقیت دریافت شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to list items for account {AccountId}.", accountId);
            return Result<CloudItemListDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure listing items for account {AccountId}.", accountId);
            return Result<CloudItemListDto>.Failed("دریافت لیست فایل‌ها ناموفق بود.");
        }
    }

    public async Task<Result<CloudItemDto>> GetItemAsync(
        string userId,
        Guid accountId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return Result<CloudItemDto>.Failed("شناسه آیتم الزامی است.");
        }

        var access = await ResolveAccessAsync(userId, accountId, cancellationToken);
        if (!access.Success)
        {
            return Result<CloudItemDto>.Failed(access.ErrorMessage!);
        }

        try
        {
            var item = await access.FileProvider!.GetItemAsync(access.AccessToken!, itemId, cancellationToken);
            return Result<CloudItemDto>.Succeeded(CloudItemDto.FromDomain(item), "جزئیات آیتم با موفقیت دریافت شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to get item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<CloudItemDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure getting item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<CloudItemDto>.Failed("دریافت جزئیات آیتم ناموفق بود.");
        }
    }

    public async Task<Result<CloudItemDto>> UploadFileAsync(
        string userId,
        Guid accountId,
        UploadCloudFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return Result<CloudItemDto>.Failed("نام فایل الزامی است.");
        }

        if (request.Content is null || request.Content == Stream.Null)
        {
            return Result<CloudItemDto>.Failed("فایل برای آپلود الزامی است.");
        }

        if (request.ContentLength is 0 || (request.Content.CanSeek && request.Content.Length == 0))
        {
            return Result<CloudItemDto>.Failed("آپلود فایل خالی مجاز نیست.");
        }

        var access = await ResolveAccessAsync(userId, accountId, cancellationToken);
        if (!access.Success)
        {
            return Result<CloudItemDto>.Failed(access.ErrorMessage!);
        }

        try
        {
            request.ParentFolderId = NormalizeParentId(request.ParentFolderId, access.Account!.RootFolderId);
            var uploaded = await access.FileProvider!.UploadFileAsync(access.AccessToken!, request, cancellationToken);
            return Result<CloudItemDto>.Succeeded(CloudItemDto.FromDomain(uploaded), "فایل با موفقیت آپلود شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to upload file for account {AccountId}.", accountId);
            return Result<CloudItemDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure uploading file for account {AccountId}.", accountId);
            return Result<CloudItemDto>.Failed("آپلود فایل ناموفق بود.");
        }
    }

    public async Task<Result<CloudDownloadResult>> DownloadFileAsync(
        string userId,
        Guid accountId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return Result<CloudDownloadResult>.Failed("شناسه فایل الزامی است.");
        }

        var access = await ResolveAccessAsync(userId, accountId, cancellationToken);
        if (!access.Success)
        {
            return Result<CloudDownloadResult>.Failed(access.ErrorMessage!);
        }

        try
        {
            var download = await access.FileProvider!.DownloadFileAsync(access.AccessToken!, itemId, cancellationToken);
            return Result<CloudDownloadResult>.Succeeded(download, "دانلود فایل آماده است.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to download item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<CloudDownloadResult>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure downloading item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<CloudDownloadResult>.Failed("دانلود فایل ناموفق بود.");
        }
    }

    public async Task<Result<object>> DeleteItemAsync(
        string userId,
        Guid accountId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return Result<object>.Failed("شناسه آیتم الزامی است.");
        }

        var access = await ResolveAccessAsync(userId, accountId, cancellationToken);
        if (!access.Success)
        {
            return Result<object>.Failed(access.ErrorMessage!);
        }

        try
        {
            await access.FileProvider!.DeleteItemAsync(access.AccessToken!, itemId, cancellationToken);
            return Result<object>.Succeeded(new { }, "آیتم با موفقیت حذف شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to delete item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<object>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure deleting item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<object>.Failed("حذف آیتم ناموفق بود.");
        }
    }

    public async Task<Result<CloudItemDto>> CreateFolderAsync(
        string userId,
        Guid accountId,
        CreateCloudFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await ValidationHelper.ValidateAsync(_createFolderValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<CloudItemDto>.Failed("اعتبارسنجی ناموفق بود.", validationErrors);
        }

        var access = await ResolveAccessAsync(userId, accountId, cancellationToken);
        if (!access.Success)
        {
            return Result<CloudItemDto>.Failed(access.ErrorMessage!);
        }

        try
        {
            request.ParentFolderId = NormalizeParentId(request.ParentFolderId, access.Account!.RootFolderId);
            var folder = await access.FileProvider!.CreateFolderAsync(access.AccessToken!, request, cancellationToken);
            return Result<CloudItemDto>.Succeeded(CloudItemDto.FromDomain(folder), "پوشه با موفقیت ایجاد شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to create folder for account {AccountId}.", accountId);
            return Result<CloudItemDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure creating folder for account {AccountId}.", accountId);
            return Result<CloudItemDto>.Failed("ایجاد پوشه ناموفق بود.");
        }
    }

    public async Task<Result<CloudItemDto>> MoveItemAsync(
        string userId,
        Guid accountId,
        string itemId,
        MoveCloudItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return Result<CloudItemDto>.Failed("شناسه آیتم الزامی است.");
        }

        var validationErrors = await ValidationHelper.ValidateAsync(_moveValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<CloudItemDto>.Failed("اعتبارسنجی ناموفق بود.", validationErrors);
        }

        if (string.Equals(itemId, request.DestinationParentFolderId, StringComparison.Ordinal))
        {
            return Result<CloudItemDto>.Failed("انتقال یک پوشه به داخل خودش مجاز نیست.");
        }

        var access = await ResolveAccessAsync(userId, accountId, cancellationToken);
        if (!access.Success)
        {
            return Result<CloudItemDto>.Failed(access.ErrorMessage!);
        }

        try
        {
            var destination = NormalizeParentId(request.DestinationParentFolderId, access.Account!.RootFolderId);
            var moved = await access.FileProvider!.MoveItemAsync(access.AccessToken!, itemId, destination, cancellationToken);
            return Result<CloudItemDto>.Succeeded(CloudItemDto.FromDomain(moved), "آیتم با موفقیت منتقل شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to move item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<CloudItemDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure moving item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<CloudItemDto>.Failed("انتقال آیتم ناموفق بود.");
        }
    }

    public async Task<Result<CloudItemDto>> RenameItemAsync(
        string userId,
        Guid accountId,
        string itemId,
        RenameCloudItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return Result<CloudItemDto>.Failed("شناسه آیتم الزامی است.");
        }

        var validationErrors = await ValidationHelper.ValidateAsync(_renameValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<CloudItemDto>.Failed("اعتبارسنجی ناموفق بود.", validationErrors);
        }

        var access = await ResolveAccessAsync(userId, accountId, cancellationToken);
        if (!access.Success)
        {
            return Result<CloudItemDto>.Failed(access.ErrorMessage!);
        }

        try
        {
            var renamed = await access.FileProvider!.RenameItemAsync(access.AccessToken!, itemId, request.NewName.Trim(), cancellationToken);
            return Result<CloudItemDto>.Succeeded(CloudItemDto.FromDomain(renamed), "نام آیتم با موفقیت تغییر کرد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to rename item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<CloudItemDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure renaming item {ItemId} for account {AccountId}.", itemId, accountId);
            return Result<CloudItemDto>.Failed("تغییر نام آیتم ناموفق بود.");
        }
    }

    private async Task<AccessContext> ResolveAccessAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return AccessContext.Fail("کاربر احراز هویت نشده است.");
        }

        var account = await _credentialService.GetOwnedActiveAccountAsync(userId, accountId, cancellationToken);
        if (account is null)
        {
            return AccessContext.Fail("حساب ابری مورد نظر یافت نشد.");
        }

        if (!_providerFactory.IsSupported(account.Provider))
        {
            return AccessContext.Fail("عملیات فایل برای این ارائه‌دهنده هنوز پشتیبانی نمی‌شود.");
        }

        try
        {
            var fileProvider = _providerFactory.GetFileProvider(account.Provider);
            var accessToken = await _credentialService.GetValidAccessTokenAsync(account, cancellationToken);
            return AccessContext.Ok(account, accessToken, fileProvider);
        }
        catch (InvalidOperationException exception)
        {
            return AccessContext.Fail(exception.Message);
        }
    }

    private static string NormalizeParentId(string? parentId, string? rootFolderId)
    {
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            return parentId;
        }

        // Provider-specific root identifiers are stored on ConnectedCloudAccount.RootFolderId
        // (e.g. Google "root", Dropbox "").
        return rootFolderId ?? string.Empty;
    }

    private sealed class AccessContext
    {
        public bool Success { get; private init; }
        public string? ErrorMessage { get; private init; }
        public Domain.Entities.ConnectedCloudAccount? Account { get; private init; }
        public string? AccessToken { get; private init; }
        public ICloudFileProviderClient? FileProvider { get; private init; }

        public static AccessContext Ok(
            Domain.Entities.ConnectedCloudAccount account,
            string accessToken,
            ICloudFileProviderClient fileProvider) => new()
        {
            Success = true,
            Account = account,
            AccessToken = accessToken,
            FileProvider = fileProvider
        };

        public static AccessContext Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
