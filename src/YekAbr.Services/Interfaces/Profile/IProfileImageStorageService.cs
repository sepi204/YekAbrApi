namespace YekAbr.Services.Interfaces.Profile;

/// <summary>
/// Stores profile images on local disk under wwwroot and returns relative web paths.
/// </summary>
public interface IProfileImageStorageService
{
    Task<string> SaveAsync(
        string userId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    void DeleteIfExists(string? relativeProfileImageUrl);
}
