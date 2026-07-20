using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YekAbr.Api.Extensions;
using YekAbr.Api.Models.Common;
using YekAbr.Services.DTOs.Profile;
using YekAbr.Services.Interfaces.Auth;
using YekAbr.Services.Interfaces.Profile;

namespace YekAbr.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ICurrentUserService _currentUserService;

    public ProfileController(IProfileService profileService, ICurrentUserService currentUserService)
    {
        _profileService = profileService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> GetProfile(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _profileService.GetProfileAsync(_currentUserService.UserId, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateProfileAsync(_currentUserService.UserId, request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [HttpPost("image")]
    [RequestSizeLimit(2_097_152)]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> UploadProfileImage(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        if (file is null || file.Length <= 0)
        {
            return BadRequest(ApiResponse<ProfileResponse>.FromResult(false, "فایل تصویر الزامی است."));
        }

        await using var stream = file.OpenReadStream();
        var result = await _profileService.UploadProfileImageAsync(
            _currentUserService.UserId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            cancellationToken);

        return this.ToApiResponse(result);
    }

    [HttpDelete("image")]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> DeleteProfileImage(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _profileService.DeleteProfileImageAsync(_currentUserService.UserId, cancellationToken);
        return this.ToApiResponse(result);
    }
}
