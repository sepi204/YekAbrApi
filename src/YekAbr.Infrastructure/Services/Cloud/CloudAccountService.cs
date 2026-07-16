using Microsoft.Extensions.Logging;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Interfaces;
using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Infrastructure.Services.Cloud;

public sealed class CloudAccountService : ICloudAccountService
{
    private readonly IConnectedCloudAccountRepository _accountRepository;
    private readonly ICloudProviderClientFactory _providerFactory;
    private readonly IGoogleDriveProviderClient _googleDriveProvider;
    private readonly ICloudAccountCredentialService _credentialService;
    private readonly ILogger<CloudAccountService> _logger;

    public CloudAccountService(
        IConnectedCloudAccountRepository accountRepository,
        ICloudProviderClientFactory providerFactory,
        IGoogleDriveProviderClient googleDriveProvider,
        ICloudAccountCredentialService credentialService,
        ILogger<CloudAccountService> logger)
    {
        _accountRepository = accountRepository;
        _providerFactory = providerFactory;
        _googleDriveProvider = googleDriveProvider;
        _credentialService = credentialService;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<ConnectedCloudAccountDto>>> ListAccountsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<IReadOnlyList<ConnectedCloudAccountDto>>.Failed("کاربر احراز هویت نشده است.");
        }

        var accounts = await _accountRepository.GetByUserIdAsync(userId, cancellationToken);
        var activeAccounts = accounts
            .Where(x => x.IsActive)
            .Select(MapAccount)
            .ToList();

        return Result<IReadOnlyList<ConnectedCloudAccountDto>>.Succeeded(
            activeAccounts,
            "لیست حساب‌های ابری با موفقیت دریافت شد.");
    }

    public async Task<Result<object>> DisconnectAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<object>.Failed("کاربر احراز هویت نشده است.");
        }

        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.UserId != userId)
        {
            return Result<object>.Failed("حساب ابری مورد نظر یافت نشد.");
        }

        if (!account.IsActive)
        {
            return Result<object>.Succeeded(new { }, "حساب ابری قبلاً قطع اتصال شده است.");
        }

        account.IsActive = false;
        account.AccessToken = string.Empty;
        account.RefreshToken = null;
        account.AccessTokenExpiresAtUtc = null;
        account.UpdatedAtUtc = DateTime.UtcNow;

        _accountRepository.Update(account);
        await _accountRepository.SaveChangesAsync(cancellationToken);

        return Result<object>.Succeeded(new { }, "اتصال حساب ابری با موفقیت قطع شد.");
    }

    public async Task<Result<CloudStorageUsageDto>> GetUsageAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<CloudStorageUsageDto>.Failed("کاربر احراز هویت نشده است.");
        }

        var account = await _credentialService.GetOwnedActiveAccountAsync(userId, accountId, cancellationToken);
        if (account is null)
        {
            return Result<CloudStorageUsageDto>.Failed("حساب ابری مورد نظر یافت نشد.");
        }

        if (account.Provider != CloudProviderType.GoogleDrive)
        {
            return Result<CloudStorageUsageDto>.Failed("دریافت فضای ذخیره‌سازی برای این ارائه‌دهنده هنوز پشتیبانی نمی‌شود.");
        }

        try
        {
            var accessToken = await _credentialService.GetValidAccessTokenAsync(account, cancellationToken);
            var usage = await _googleDriveProvider.GetStorageUsageAsync(accessToken, cancellationToken);

            account.LastSyncedAtUtc = DateTime.UtcNow;
            account.UpdatedAtUtc = DateTime.UtcNow;
            _accountRepository.Update(account);
            await _accountRepository.SaveChangesAsync(cancellationToken);

            return Result<CloudStorageUsageDto>.Succeeded(
                new CloudStorageUsageDto
                {
                    TotalBytes = usage.TotalBytes,
                    UsedBytes = usage.UsedBytes,
                    FreeBytes = usage.FreeBytes
                },
                "میزان فضای ذخیره‌سازی با موفقیت دریافت شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to retrieve Google Drive usage for account {AccountId}.", accountId);
            return Result<CloudStorageUsageDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected failure retrieving Google Drive usage for account {AccountId}.", accountId);
            return Result<CloudStorageUsageDto>.Failed("دریافت میزان فضای ذخیره‌سازی ناموفق بود.");
        }
    }

    private ConnectedCloudAccountDto MapAccount(Domain.Entities.ConnectedCloudAccount account)
    {
        var providerName = _providerFactory.IsSupported(account.Provider)
            ? _providerFactory.GetProvider(account.Provider).ProviderName
            : account.Provider.ToString();

        return new ConnectedCloudAccountDto
        {
            Id = account.Id,
            Provider = account.Provider,
            ProviderName = providerName,
            AccountEmail = account.AccountEmail,
            DisplayName = account.DisplayName,
            IsActive = account.IsActive,
            CreatedAtUtc = account.CreatedAtUtc,
            LastSyncedAtUtc = account.LastSyncedAtUtc
        };
    }
}
