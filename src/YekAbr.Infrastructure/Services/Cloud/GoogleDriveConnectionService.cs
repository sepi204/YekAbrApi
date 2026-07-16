using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YekAbr.Domain.Entities;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Interfaces;
using YekAbr.Infrastructure.Cloud.GoogleDrive;
using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Infrastructure.Services.Cloud;

public sealed class GoogleDriveConnectionService : IGoogleDriveConnectionService
{
    private readonly IGoogleDriveProviderClient _googleDriveProvider;
    private readonly ICloudOAuthStateStore _oauthStateStore;
    private readonly IConnectedCloudAccountRepository _accountRepository;
    private readonly ICloudTokenEncryptionService _tokenEncryptionService;
    private readonly GoogleDriveOptions _options;
    private readonly ILogger<GoogleDriveConnectionService> _logger;

    public GoogleDriveConnectionService(
        IGoogleDriveProviderClient googleDriveProvider,
        ICloudOAuthStateStore oauthStateStore,
        IConnectedCloudAccountRepository accountRepository,
        ICloudTokenEncryptionService tokenEncryptionService,
        IOptions<GoogleDriveOptions> options,
        ILogger<GoogleDriveConnectionService> logger)
    {
        _googleDriveProvider = googleDriveProvider;
        _oauthStateStore = oauthStateStore;
        _accountRepository = accountRepository;
        _tokenEncryptionService = tokenEncryptionService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<GoogleConnectUrlDto>> GetConnectUrlAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<GoogleConnectUrlDto>.Failed("کاربر احراز هویت نشده است.");
        }

        try
        {
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var lifetime = TimeSpan.FromMinutes(Math.Max(1, _options.OAuthStateLifetimeMinutes));

            await _oauthStateStore.StoreAsync(state, userId, lifetime, cancellationToken);

            var authorizationUrl = _googleDriveProvider.BuildAuthorizationUrl(state);
            return Result<GoogleConnectUrlDto>.Succeeded(
                new GoogleConnectUrlDto { AuthorizationUrl = authorizationUrl },
                "لینک اتصال به گوگل درایو با موفقیت ایجاد شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to build Google Drive connect URL.");
            return Result<GoogleConnectUrlDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while building Google Drive connect URL.");
            return Result<GoogleConnectUrlDto>.Failed("ایجاد لینک اتصال گوگل درایو ناموفق بود.");
        }
    }

    public async Task<Result<ConnectedCloudAccountDto>> HandleCallbackAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Result<ConnectedCloudAccountDto>.Failed("اتصال به گوگل درایو توسط کاربر لغو شد یا با خطا مواجه شد.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<ConnectedCloudAccountDto>.Failed("کد مجوز گوگل دریافت نشد.");
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            return Result<ConnectedCloudAccountDto>.Failed("پارامتر امنیتی state نامعتبر است.");
        }

        var userId = await _oauthStateStore.ConsumeAsync(state, cancellationToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<ConnectedCloudAccountDto>.Failed("پارامتر امنیتی state منقضی یا نامعتبر است.");
        }

        try
        {
            var tokenResult = await _googleDriveProvider.ExchangeAuthorizationCodeAsync(code, cancellationToken);
            var accountInfo = await _googleDriveProvider.GetAccountInfoAsync(tokenResult.AccessToken, cancellationToken);

            var existing = await _accountRepository.GetByUserIdAndProviderAccountIdAsync(
                userId,
                CloudProviderType.GoogleDrive,
                accountInfo.ProviderAccountId,
                cancellationToken);

            var now = DateTime.UtcNow;
            var encryptedAccessToken = _tokenEncryptionService.Encrypt(tokenResult.AccessToken);
            string? encryptedRefreshToken = null;
            if (!string.IsNullOrWhiteSpace(tokenResult.RefreshToken))
            {
                encryptedRefreshToken = _tokenEncryptionService.Encrypt(tokenResult.RefreshToken);
            }

            ConnectedCloudAccount account;
            if (existing is null)
            {
                account = new ConnectedCloudAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Provider = CloudProviderType.GoogleDrive,
                    AccountEmail = accountInfo.Email,
                    DisplayName = accountInfo.DisplayName,
                    ProviderAccountId = accountInfo.ProviderAccountId,
                    AccessToken = encryptedAccessToken,
                    RefreshToken = encryptedRefreshToken,
                    AccessTokenExpiresAtUtc = tokenResult.AccessTokenExpiresAtUtc,
                    RootFolderId = "root",
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    LastSyncedAtUtc = now
                };

                await _accountRepository.AddAsync(account, cancellationToken);
            }
            else
            {
                account = existing;
                account.AccountEmail = accountInfo.Email;
                account.DisplayName = accountInfo.DisplayName;
                account.AccessToken = encryptedAccessToken;
                if (!string.IsNullOrWhiteSpace(encryptedRefreshToken))
                {
                    account.RefreshToken = encryptedRefreshToken;
                }

                account.AccessTokenExpiresAtUtc = tokenResult.AccessTokenExpiresAtUtc;
                account.RootFolderId ??= "root";
                account.IsActive = true;
                account.UpdatedAtUtc = now;
                account.LastSyncedAtUtc = now;
                _accountRepository.Update(account);
            }

            await _accountRepository.SaveChangesAsync(cancellationToken);

            return Result<ConnectedCloudAccountDto>.Succeeded(
                MapAccount(account, _googleDriveProvider.ProviderName),
                "حساب گوگل درایو با موفقیت متصل شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Google Drive OAuth callback failed.");
            return Result<ConnectedCloudAccountDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected Google Drive OAuth callback failure.");
            return Result<ConnectedCloudAccountDto>.Failed("اتصال حساب گوگل درایو ناموفق بود.");
        }
    }

    internal static ConnectedCloudAccountDto MapAccount(ConnectedCloudAccount account, string providerName)
    {
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
