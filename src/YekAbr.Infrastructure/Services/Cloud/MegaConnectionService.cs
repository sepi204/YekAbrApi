using FluentValidation;
using Microsoft.Extensions.Logging;
using YekAbr.Domain.Entities;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Interfaces;
using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Cloud;
using YekAbr.Services.Services.Auth;

namespace YekAbr.Infrastructure.Services.Cloud;

public sealed class MegaConnectionService : IMegaConnectionService
{
    private readonly IMegaProviderClient _megaProvider;
    private readonly IConnectedCloudAccountRepository _accountRepository;
    private readonly ICloudTokenEncryptionService _tokenEncryptionService;
    private readonly IValidator<ConnectMegaAccountRequest> _connectValidator;
    private readonly ILogger<MegaConnectionService> _logger;

    public MegaConnectionService(
        IMegaProviderClient megaProvider,
        IConnectedCloudAccountRepository accountRepository,
        ICloudTokenEncryptionService tokenEncryptionService,
        IValidator<ConnectMegaAccountRequest> connectValidator,
        ILogger<MegaConnectionService> logger)
    {
        _megaProvider = megaProvider;
        _accountRepository = accountRepository;
        _tokenEncryptionService = tokenEncryptionService;
        _connectValidator = connectValidator;
        _logger = logger;
    }

    public async Task<Result<ConnectedCloudAccountDto>> ConnectAsync(
        string userId,
        ConnectMegaAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<ConnectedCloudAccountDto>.Failed("کاربر احراز هویت نشده است.");
        }

        var validationErrors = await ValidationHelper.ValidateAsync(_connectValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<ConnectedCloudAccountDto>.Failed("اعتبارسنجی ناموفق بود.", validationErrors);
        }

        try
        {
            var material = await _megaProvider.CreateConnectionMaterialAsync(
                request.Email,
                request.Password,
                request.MfaKey,
                cancellationToken);

            var existing = await _accountRepository.GetByUserIdAndProviderAccountIdAsync(
                userId,
                CloudProviderType.Mega,
                material.AccountInfo.ProviderAccountId,
                cancellationToken);

            var now = DateTime.UtcNow;
            var encryptedAuthInfos = _tokenEncryptionService.Encrypt(material.AuthInfosJson);

            ConnectedCloudAccount account;
            if (existing is null)
            {
                account = new ConnectedCloudAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Provider = CloudProviderType.Mega,
                    AccountEmail = material.AccountInfo.Email,
                    DisplayName = material.AccountInfo.DisplayName,
                    ProviderAccountId = material.AccountInfo.ProviderAccountId,
                    AccessToken = encryptedAuthInfos,
                    RefreshToken = null,
                    AccessTokenExpiresAtUtc = null,
                    RootFolderId = material.RootFolderId,
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
                account.AccountEmail = material.AccountInfo.Email;
                account.DisplayName = material.AccountInfo.DisplayName;
                account.AccessToken = encryptedAuthInfos;
                account.RefreshToken = null;
                account.AccessTokenExpiresAtUtc = null;
                account.RootFolderId = material.RootFolderId;
                account.IsActive = true;
                account.UpdatedAtUtc = now;
                account.LastSyncedAtUtc = now;
                _accountRepository.Update(account);
            }

            await _accountRepository.SaveChangesAsync(cancellationToken);

            return Result<ConnectedCloudAccountDto>.Succeeded(
                MapAccount(account),
                "حساب مگا با موفقیت متصل شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "MEGA connect failed.");
            return Result<ConnectedCloudAccountDto>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected MEGA connect failure.");
            return Result<ConnectedCloudAccountDto>.Failed("اتصال حساب مگا ناموفق بود.");
        }
    }

    private ConnectedCloudAccountDto MapAccount(ConnectedCloudAccount account)
    {
        return new ConnectedCloudAccountDto
        {
            Id = account.Id,
            Provider = account.Provider,
            ProviderName = _megaProvider.ProviderName,
            AccountEmail = account.AccountEmail,
            DisplayName = account.DisplayName,
            IsActive = account.IsActive,
            CreatedAtUtc = account.CreatedAtUtc,
            LastSyncedAtUtc = account.LastSyncedAtUtc
        };
    }
}
