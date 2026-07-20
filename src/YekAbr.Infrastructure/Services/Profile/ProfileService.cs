using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YekAbr.Infrastructure.Identity;
using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Profile;
using YekAbr.Services.Interfaces.Profile;
using YekAbr.Services.Services.Auth;

namespace YekAbr.Infrastructure.Services.Profile;

public sealed class ProfileService : IProfileService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IProfileImageStorageService _imageStorage;
    private readonly IPublicUrlBuilder _publicUrlBuilder;
    private readonly IValidator<UpdateProfileRequest> _updateValidator;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        UserManager<AppUser> userManager,
        IProfileImageStorageService imageStorage,
        IPublicUrlBuilder publicUrlBuilder,
        IValidator<UpdateProfileRequest> updateValidator,
        ILogger<ProfileService> logger)
    {
        _userManager = userManager;
        _imageStorage = imageStorage;
        _publicUrlBuilder = publicUrlBuilder;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public async Task<Result<ProfileResponse>> GetProfileAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<ProfileResponse>.Failed("کاربر احراز هویت نشده است.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result<ProfileResponse>.Failed("کاربر یافت نشد.");
        }

        return Result<ProfileResponse>.Succeeded(
            MapProfile(user),
            "اطلاعات پروفایل با موفقیت دریافت شد.");
    }

    public async Task<Result<ProfileResponse>> UpdateProfileAsync(
        string userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<ProfileResponse>.Failed("کاربر احراز هویت نشده است.");
        }

        var validationErrors = await ValidationHelper.ValidateAsync(_updateValidator, request, cancellationToken);
        if (validationErrors is not null)
        {
            return Result<ProfileResponse>.Failed("اعتبارسنجی ناموفق بود.", validationErrors);
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result<ProfileResponse>.Failed("کاربر یافت نشد.");
        }

        var newUsername = request.Username.Trim();
        var newEmail = request.Email.Trim();

        if (!string.Equals(user.UserName, newUsername, StringComparison.Ordinal))
        {
            var usernameTaken = await _userManager.Users.AnyAsync(
                x => x.Id != userId && x.NormalizedUserName == newUsername.ToUpperInvariant(),
                cancellationToken);

            if (usernameTaken)
            {
                return Result<ProfileResponse>.Failed(
                    "این نام کاربری قبلاً توسط کاربر دیگری ثبت شده است.",
                    new Dictionary<string, string[]>
                    {
                        [nameof(request.Username)] = ["این نام کاربری قبلاً توسط کاربر دیگری ثبت شده است."]
                    });
            }

            var setUserNameResult = await _userManager.SetUserNameAsync(user, newUsername);
            if (!setUserNameResult.Succeeded)
            {
                return Result<ProfileResponse>.Failed(
                    "بروزرسانی نام کاربری ناموفق بود.",
                    MapIdentityErrors(setUserNameResult.Errors));
            }
        }

        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailTaken = await _userManager.Users.AnyAsync(
                x => x.Id != userId && x.NormalizedEmail == newEmail.ToUpperInvariant(),
                cancellationToken);

            if (emailTaken)
            {
                return Result<ProfileResponse>.Failed(
                    "این ایمیل قبلاً توسط کاربر دیگری ثبت شده است.",
                    new Dictionary<string, string[]>
                    {
                        [nameof(request.Email)] = ["این ایمیل قبلاً توسط کاربر دیگری ثبت شده است."]
                    });
            }

            var setEmailResult = await _userManager.SetEmailAsync(user, newEmail);
            if (!setEmailResult.Succeeded)
            {
                return Result<ProfileResponse>.Failed(
                    "بروزرسانی ایمیل ناموفق بود.",
                    MapIdentityErrors(setEmailResult.Errors));
            }
        }

        // Reload after UserManager mutations to return fresh values.
        var refreshed = await _userManager.FindByIdAsync(userId) ?? user;
        return Result<ProfileResponse>.Succeeded(
            MapProfile(refreshed),
            "پروفایل با موفقیت بروزرسانی شد.");
    }

    public async Task<Result<ProfileResponse>> UploadProfileImageAsync(
        string userId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<ProfileResponse>.Failed("کاربر احراز هویت نشده است.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result<ProfileResponse>.Failed("کاربر یافت نشد.");
        }

        try
        {
            var previousImage = user.ProfileImageUrl;
            var relativePath = await _imageStorage.SaveAsync(
                userId,
                content,
                fileName,
                contentType,
                contentLength,
                cancellationToken);

            user.ProfileImageUrl = relativePath;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _imageStorage.DeleteIfExists(relativePath);
                return Result<ProfileResponse>.Failed(
                    "بروزرسانی تصویر پروفایل ناموفق بود.",
                    MapIdentityErrors(updateResult.Errors));
            }

            if (!string.Equals(previousImage, relativePath, StringComparison.OrdinalIgnoreCase))
            {
                _imageStorage.DeleteIfExists(previousImage);
            }

            return Result<ProfileResponse>.Succeeded(
                MapProfile(user),
                "تصویر پروفایل با موفقیت بروزرسانی شد.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Profile image upload validation failed for user {UserId}.", userId);
            return Result<ProfileResponse>.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected profile image upload failure for user {UserId}.", userId);
            return Result<ProfileResponse>.Failed("آپلود تصویر پروفایل ناموفق بود.");
        }
    }

    public async Task<Result<ProfileResponse>> DeleteProfileImageAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<ProfileResponse>.Failed("کاربر احراز هویت نشده است.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result<ProfileResponse>.Failed("کاربر یافت نشد.");
        }

        if (string.IsNullOrWhiteSpace(user.ProfileImageUrl))
        {
            return Result<ProfileResponse>.Succeeded(
                MapProfile(user),
                "تصویر پروفایلی برای حذف وجود ندارد.");
        }

        var previousImage = user.ProfileImageUrl;
        user.ProfileImageUrl = null;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result<ProfileResponse>.Failed(
                "حذف تصویر پروفایل ناموفق بود.",
                MapIdentityErrors(updateResult.Errors));
        }

        _imageStorage.DeleteIfExists(previousImage);

        return Result<ProfileResponse>.Succeeded(
            MapProfile(user),
            "تصویر پروفایل با موفقیت حذف شد.");
    }

    private ProfileResponse MapProfile(AppUser user)
    {
        return new ProfileResponse
        {
            Id = user.Id,
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            ProfileImageUrl = _publicUrlBuilder.ToAbsoluteUrl(user.ProfileImageUrl)
        };
    }

    private static IReadOnlyDictionary<string, string[]> MapIdentityErrors(IEnumerable<IdentityError> errors)
    {
        return errors
            .GroupBy(x => x.Code)
            .ToDictionary(
                x => x.Key,
                x => x.Select(TranslateIdentityError).Distinct().ToArray() as string[]);
    }

    private static string TranslateIdentityError(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateUserName" => "این نام کاربری قبلاً توسط کاربر دیگری ثبت شده است.",
            "DuplicateEmail" => "این ایمیل قبلاً توسط کاربر دیگری ثبت شده است.",
            "InvalidUserName" => "نام کاربری معتبر نیست.",
            "InvalidEmail" => "ایمیل معتبر نیست.",
            _ => "بروزرسانی پروفایل ناموفق بود."
        };
    }
}
