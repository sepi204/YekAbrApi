using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YekAbr.Infrastructure.Options;
using YekAbr.Services.Interfaces.Profile;

namespace YekAbr.Infrastructure.Services.Profile;

public sealed class LocalProfileImageStorageService : IProfileImageStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ProfileImageOptions _options;
    private readonly ILogger<LocalProfileImageStorageService> _logger;

    public LocalProfileImageStorageService(
        IWebHostEnvironment environment,
        IOptions<ProfileImageOptions> options,
        ILogger<LocalProfileImageStorageService> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> SaveAsync(
        string userId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("شناسه کاربر نامعتبر است.");
        }

        if (content is null || contentLength <= 0)
        {
            throw new InvalidOperationException("فایل تصویر الزامی است.");
        }

        if (contentLength > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException("حجم تصویر نباید بیشتر از ۲ مگابایت باشد.");
        }

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("فرمت تصویر پشتیبانی نمی‌شود. فقط JPG، PNG و WEBP مجاز هستند.");
        }

        if (!string.IsNullOrWhiteSpace(contentType)
            && !_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("نوع فایل تصویر معتبر نیست. فقط JPG، PNG و WEBP مجاز هستند.");
        }

        var webRoot = EnsureWebRoot();
        var relativeFolder = _options.RelativeUploadPath.Trim('/').Replace('\\', '/');
        var absoluteFolder = Path.Combine(webRoot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(absoluteFolder);

        var storedFileName = $"{userId}_{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteFolder, storedFileName);

        await using (var fileStream = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        return "/" + relativeFolder + "/" + storedFileName;
    }

    public void DeleteIfExists(string? relativeProfileImageUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeProfileImageUrl))
        {
            return;
        }

        try
        {
            var webRoot = EnsureWebRoot();
            var normalized = relativeProfileImageUrl.Trim().Replace('\\', '/').TrimStart('/');
            var absolutePath = Path.Combine(webRoot, normalized.Replace('/', Path.DirectorySeparatorChar));

            // Prevent path traversal outside the configured upload folder.
            var uploadRoot = Path.GetFullPath(Path.Combine(
                webRoot,
                _options.RelativeUploadPath.Trim('/').Replace('/', Path.DirectorySeparatorChar)));
            var fullPath = Path.GetFullPath(absolutePath);
            if (!fullPath.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Refused to delete profile image outside upload root: {Path}", relativeProfileImageUrl);
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete profile image {Path}.", relativeProfileImageUrl);
        }
    }

    private string EnsureWebRoot()
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(webRoot);
        }

        return webRoot;
    }
}
