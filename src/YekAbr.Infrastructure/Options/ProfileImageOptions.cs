namespace YekAbr.Infrastructure.Options;

public sealed class ProfileImageOptions
{
    public const string SectionName = "ProfileImage";

    /// <summary>
    /// Relative folder under wwwroot where profile images are stored.
    /// </summary>
    public string RelativeUploadPath { get; set; } = "uploads/profiles";

    /// <summary>
    /// Maximum allowed upload size in bytes (default 2 MB).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 2 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    /// <summary>
    /// Optional absolute API base URL used when HttpContext is unavailable.
    /// Example: https://localhost:7184
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
