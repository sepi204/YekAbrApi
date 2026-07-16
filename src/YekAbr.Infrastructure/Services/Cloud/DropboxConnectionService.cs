using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YekAbr.Domain.Entities;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Interfaces;
using YekAbr.Infrastructure.Cloud.Dropbox;
using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Infrastructure.Services.Cloud;

public sealed class DropboxConnectionService : IDropboxConnectionService
{
    private readonly IDropboxProviderClient _dropboxProvider;
    private readonly ICloudOAuthStateStore _oauthStateStore;
    private readonly IConnectedCloudAccountRepository _accountRepository;
    private readonly ICloudTokenEncryptionService _tokenEncryptionService;
    private readonly DropboxOptions _options;
    private readonly ILogger<DropboxConnectionService> _logger;

    public DropboxConnectionService(
        IDropboxProviderClient dropboxProvider,
        ICloudOAuthStateStore oauthStateStore,
        IConnectedCloudAccountRepository accountRepository,
        ICloudTokenEncryptionService tokenEncryptionService,
        IOptions<DropboxOptions> options,
        ILogger<DropboxConnectionService> logger)
    {
        _dropboxProvider = dropboxProvider;
        _oauthStateStore = oauthStateStore;
        _accountRepository = accountRepository;
        _tokenEncryptionService = tokenEncryptionService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<CloudConnectUrlDto>> GetConnectUrlAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<CloudConnectUrlDto>.Failed("کاربر احراز هویت نشده است.");
        }

        try
        {
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var lifetime = TimeSpan.FromMinutes(Math.Max(1, _options.OAuthStateLifetimeMinutes));

            await _oauthStateStore.StoreAsync(state, userId, lifetime, cancellationToken);

            var authorizationUrl = _dropboxProvider.BuildAuthorizationUrl(state);
            return Result<CloudConnectUrlDto>.Succeeded(
                new CloudConnectUrlDto { AuthorizationUrl = authorizationUrl },
                "لینک اتصال به دراپ‌باکس با موفقیت ایجاد شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Failed to build Dropbox connect URL.");
            return Result<CloudConnectUrlDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected error while building Dropbox connect URL.");
            return Result<CloudConnectUrlDto>.Failed("ایجاد لینک اتصال دراپ‌باکس ناموفق بود.");
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
            return Result<ConnectedCloudAccountDto>.Failed("اتصال به دراپ‌باکس توسط کاربر لغو شد یا با خطا مواجه شد.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<ConnectedCloudAccountDto>.Failed("کد مجوز دراپ‌باکس دریافت نشد.");
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
            var tokenResult = await _dropboxProvider.ExchangeAuthorizationCodeAsync(code, cancellationToken);
            var accountInfo = await _dropboxProvider.GetAccountInfoAsync(tokenResult.AccessToken, cancellationToken);

            var existing = await _accountRepository.GetByUserIdAndProviderAccountIdAsync(
                userId,
                CloudProviderType.Dropbox,
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
                    Provider = CloudProviderType.Dropbox,
                    AccountEmail = accountInfo.Email,
                    DisplayName = accountInfo.DisplayName,
                    ProviderAccountId = accountInfo.ProviderAccountId,
                    AccessToken = encryptedAccessToken,
                    RefreshToken = encryptedRefreshToken,
                    AccessTokenExpiresAtUtc = tokenResult.AccessTokenExpiresAtUtc,
                    // Dropbox root is represented as an empty path.
                    RootFolderId = string.Empty,
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
                account.RootFolderId ??= string.Empty;
                account.IsActive = true;
                account.UpdatedAtUtc = now;
                account.LastSyncedAtUtc = now;
                _accountRepository.Update(account);
            }

            await _accountRepository.SaveChangesAsync(cancellationToken);

            return Result<ConnectedCloudAccountDto>.Succeeded(
                MapAccount(account, _dropboxProvider.ProviderName),
                "حساب دراپ‌باکس با موفقیت متصل شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Dropbox OAuth callback failed.");
            return Result<ConnectedCloudAccountDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected Dropbox OAuth callback failure.");
            return Result<ConnectedCloudAccountDto>.Failed("اتصال حساب دراپ‌باکس ناموفق بود.");
        }
    }

    private static ConnectedCloudAccountDto MapAccount(ConnectedCloudAccount account, string providerName)
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
