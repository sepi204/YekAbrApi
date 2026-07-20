using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Profile;

namespace YekAbr.Services.Interfaces.Profile;

public interface IProfileService
{
    Task<Result<ProfileResponse>> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result<ProfileResponse>> UpdateProfileAsync(
        string userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProfileResponse>> UploadProfileImageAsync(
        string userId,
        Stream content,
        string fileName,
        string? contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<Result<ProfileResponse>> DeleteProfileImageAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
