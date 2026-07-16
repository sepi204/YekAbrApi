using Microsoft.Extensions.Logging;
using YekAbr.Domain.Entities;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Interfaces;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Infrastructure.Services.Cloud;

public sealed class CloudAccountCredentialService : ICloudAccountCredentialService
{
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromMinutes(2);

    private readonly IConnectedCloudAccountRepository _accountRepository;
    private readonly IGoogleDriveProviderClient _googleDriveProvider;
    private readonly ICloudTokenEncryptionService _tokenEncryptionService;
    private readonly ILogger<CloudAccountCredentialService> _logger;

    public CloudAccountCredentialService(
        IConnectedCloudAccountRepository accountRepository,
        IGoogleDriveProviderClient googleDriveProvider,
        ICloudTokenEncryptionService tokenEncryptionService,
        ILogger<CloudAccountCredentialService> logger)
    {
        _accountRepository = accountRepository;
        _googleDriveProvider = googleDriveProvider;
        _tokenEncryptionService = tokenEncryptionService;
        _logger = logger;
    }

    public async Task<ConnectedCloudAccount?> GetOwnedActiveAccountAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null || account.UserId != userId || !account.IsActive)
        {
            return null;
        }

        return account;
    }

    public async Task<string> GetValidAccessTokenAsync(
        ConnectedCloudAccount account,
        CancellationToken cancellationToken = default)
    {
        if (account.Provider != CloudProviderType.GoogleDrive)
        {
            throw new InvalidOperationException("تازه‌سازی توکن برای این ارائه‌دهنده هنوز پشتیبانی نمی‌شود.");
        }

        var needsRefresh = !account.AccessTokenExpiresAtUtc.HasValue
            || account.AccessTokenExpiresAtUtc.Value <= DateTime.UtcNow.Add(TokenRefreshSkew);

        if (!needsRefresh)
        {
            if (string.IsNullOrWhiteSpace(account.AccessToken))
            {
                throw new InvalidOperationException("توکن دسترسی حساب ابری موجود نیست. لطفاً دوباره متصل شوید.");
            }

            return _tokenEncryptionService.Decrypt(account.AccessToken);
        }

        if (string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            throw new InvalidOperationException("توکن دسترسی منقضی شده و توکن تازه‌سازی موجود نیست. لطفاً دوباره متصل شوید.");
        }

        _logger.LogInformation("Refreshing Google Drive access token for account {AccountId}.", account.Id);

        var refreshToken = _tokenEncryptionService.Decrypt(account.RefreshToken);
        var refreshed = await _googleDriveProvider.RefreshAccessTokenAsync(refreshToken, cancellationToken);

        account.AccessToken = _tokenEncryptionService.Encrypt(refreshed.AccessToken);
        account.AccessTokenExpiresAtUtc = refreshed.AccessTokenExpiresAtUtc;
        if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
        {
            account.RefreshToken = _tokenEncryptionService.Encrypt(refreshed.RefreshToken);
        }

        account.UpdatedAtUtc = DateTime.UtcNow;
        _accountRepository.Update(account);
        await _accountRepository.SaveChangesAsync(cancellationToken);

        return refreshed.AccessToken;
    }
}
