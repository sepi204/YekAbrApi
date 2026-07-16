using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using YekAbr.Api.Extensions;
using YekAbr.Api.Models.Common;
using YekAbr.Infrastructure.Cloud.GoogleDrive;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Auth;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Api.Controllers;

[ApiController]
[Route("api/cloud")]
public sealed class CloudController : ControllerBase
{
    private readonly IGoogleDriveConnectionService _googleDriveConnectionService;
    private readonly ICloudAccountService _cloudAccountService;
    private readonly ICurrentUserService _currentUserService;
    private readonly GoogleDriveOptions _googleDriveOptions;

    public CloudController(
        IGoogleDriveConnectionService googleDriveConnectionService,
        ICloudAccountService cloudAccountService,
        ICurrentUserService currentUserService,
        IOptions<GoogleDriveOptions> googleDriveOptions)
    {
        _googleDriveConnectionService = googleDriveConnectionService;
        _cloudAccountService = cloudAccountService;
        _currentUserService = currentUserService;
        _googleDriveOptions = googleDriveOptions.Value;
    }

    [Authorize]
    [HttpGet("google/connect-url")]
    [ProducesResponseType(typeof(ApiResponse<GoogleConnectUrlDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GoogleConnectUrlDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<GoogleConnectUrlDto>>> GetGoogleConnectUrl(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _googleDriveConnectionService.GetConnectUrlAsync(_currentUserService.UserId, cancellationToken);
        return this.ToApiResponse(result);
    }

    /// <summary>
    /// Google OAuth callback. Validates state, then redirects to configured frontend URLs when present.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("google/callback")]
    [ProducesResponseType(typeof(ApiResponse<ConnectedCloudAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConnectedCloudAccountDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var result = await _googleDriveConnectionService.HandleCallbackAsync(code, state, error, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_googleDriveOptions.FrontendSuccessRedirectUrl)
            || !string.IsNullOrWhiteSpace(_googleDriveOptions.FrontendFailureRedirectUrl))
        {
            if (result.Success
                && result.Data is not null
                && !string.IsNullOrWhiteSpace(_googleDriveOptions.FrontendSuccessRedirectUrl))
            {
                var successUrl = QueryHelpers.AddQueryString(
                    _googleDriveOptions.FrontendSuccessRedirectUrl,
                    new Dictionary<string, string?>
                    {
                        ["connected"] = "true",
                        ["accountId"] = result.Data.Id.ToString(),
                        ["provider"] = result.Data.Provider.ToString()
                    });

                return Redirect(successUrl);
            }

            if (!string.IsNullOrWhiteSpace(_googleDriveOptions.FrontendFailureRedirectUrl))
            {
                var failureUrl = QueryHelpers.AddQueryString(
                    _googleDriveOptions.FrontendFailureRedirectUrl,
                    new Dictionary<string, string?>
                    {
                        ["connected"] = "false",
                        ["message"] = result.Message
                    });

                return Redirect(failureUrl);
            }
        }

        if (result.Success)
        {
            return Ok(ApiResponse<ConnectedCloudAccountDto>.FromResult(true, result.Message, result.Data));
        }

        return BadRequest(ApiResponse<ConnectedCloudAccountDto>.FromResult(false, result.Message, errors: result.Errors));
    }

    [Authorize]
    [HttpGet("accounts")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConnectedCloudAccountDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConnectedCloudAccountDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConnectedCloudAccountDto>>>> ListAccounts(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _cloudAccountService.ListAccountsAsync(_currentUserService.UserId, cancellationToken);
        return this.ToApiResponse(result);
    }

    [Authorize]
    [HttpDelete("accounts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> DisconnectAccount(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _cloudAccountService.DisconnectAsync(_currentUserService.UserId, id, cancellationToken);
        return this.ToApiResponse(result);
    }

    [Authorize]
    [HttpGet("accounts/{id:guid}/usage")]
    [ProducesResponseType(typeof(ApiResponse<CloudStorageUsageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CloudStorageUsageDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CloudStorageUsageDto>>> GetAccountUsage(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _cloudAccountService.GetUsageAsync(_currentUserService.UserId, id, cancellationToken);
        return this.ToApiResponse(result);
    }
}
