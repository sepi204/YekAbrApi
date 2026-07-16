using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using YekAbr.Api.Extensions;
using YekAbr.Api.Models.Common;
using YekAbr.Infrastructure.Cloud.Dropbox;
using YekAbr.Infrastructure.Cloud.GoogleDrive;
using YekAbr.Services.Common.Responses;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Auth;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Api.Controllers;

[ApiController]
[Route("api/cloud")]
public sealed class CloudController : ControllerBase
{
    private readonly IGoogleDriveConnectionService _googleDriveConnectionService;
    private readonly IDropboxConnectionService _dropboxConnectionService;
    private readonly IMegaConnectionService _megaConnectionService;
    private readonly ICloudAccountService _cloudAccountService;
    private readonly ICloudFileService _cloudFileService;
    private readonly ICurrentUserService _currentUserService;
    private readonly GoogleDriveOptions _googleDriveOptions;
    private readonly DropboxOptions _dropboxOptions;

    public CloudController(
        IGoogleDriveConnectionService googleDriveConnectionService,
        IDropboxConnectionService dropboxConnectionService,
        IMegaConnectionService megaConnectionService,
        ICloudAccountService cloudAccountService,
        ICloudFileService cloudFileService,
        ICurrentUserService currentUserService,
        IOptions<GoogleDriveOptions> googleDriveOptions,
        IOptions<DropboxOptions> dropboxOptions)
    {
        _googleDriveConnectionService = googleDriveConnectionService;
        _dropboxConnectionService = dropboxConnectionService;
        _megaConnectionService = megaConnectionService;
        _cloudAccountService = cloudAccountService;
        _cloudFileService = cloudFileService;
        _currentUserService = currentUserService;
        _googleDriveOptions = googleDriveOptions.Value;
        _dropboxOptions = dropboxOptions.Value;
    }

    [Authorize]
    [HttpGet("google/connect-url")]
    [ProducesResponseType(typeof(ApiResponse<CloudConnectUrlDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CloudConnectUrlDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CloudConnectUrlDto>>> GetGoogleConnectUrl(CancellationToken cancellationToken)
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
        return HandleOAuthCallbackResult(
            result,
            _googleDriveOptions.FrontendSuccessRedirectUrl,
            _googleDriveOptions.FrontendFailureRedirectUrl);
    }

    [Authorize]
    [HttpGet("dropbox/connect-url")]
    [ProducesResponseType(typeof(ApiResponse<CloudConnectUrlDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CloudConnectUrlDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CloudConnectUrlDto>>> GetDropboxConnectUrl(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _dropboxConnectionService.GetConnectUrlAsync(_currentUserService.UserId, cancellationToken);
        return this.ToApiResponse(result);
    }

    /// <summary>
    /// Dropbox OAuth callback. Validates state, then redirects to configured frontend URLs when present.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("dropbox/callback")]
    [ProducesResponseType(typeof(ApiResponse<ConnectedCloudAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConnectedCloudAccountDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DropboxCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var result = await _dropboxConnectionService.HandleCallbackAsync(code, state, error, cancellationToken);
        return HandleOAuthCallbackResult(
            result,
            _dropboxOptions.FrontendSuccessRedirectUrl,
            _dropboxOptions.FrontendFailureRedirectUrl);
    }

    /// <summary>
    /// Connect a MEGA account with email/password (optional MFA). MEGA does not use OAuth.
    /// </summary>
    [Authorize]
    [HttpPost("mega/connect")]
    [ProducesResponseType(typeof(ApiResponse<ConnectedCloudAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConnectedCloudAccountDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ConnectedCloudAccountDto>>> ConnectMega(
        [FromBody] ConnectMegaAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _megaConnectionService.ConnectAsync(_currentUserService.UserId, request, cancellationToken);
        return this.ToApiResponse(result);
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

    [Authorize]
    [HttpGet("accounts/{id:guid}/items")]
    [ProducesResponseType(typeof(ApiResponse<CloudItemListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CloudItemListDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CloudItemListDto>>> ListItems(
        Guid id,
        [FromQuery] string? parentId,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? pageToken = null,
        [FromQuery] string? search = null,
        [FromQuery] bool includeFolders = true,
        [FromQuery] bool includeFiles = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var request = new ListCloudItemsRequest
        {
            ParentId = parentId,
            PageSize = pageSize,
            PageToken = pageToken,
            Search = search,
            IncludeFolders = includeFolders,
            IncludeFiles = includeFiles
        };

        var result = await _cloudFileService.ListItemsAsync(_currentUserService.UserId, id, request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [Authorize]
    [HttpGet("accounts/{id:guid}/items/{itemId}")]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CloudItemDto>>> GetItem(
        Guid id,
        string itemId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _cloudFileService.GetItemAsync(_currentUserService.UserId, id, itemId, cancellationToken);
        return this.ToApiResponse(result);
    }

    [Authorize]
    [HttpPost("accounts/{id:guid}/files/upload")]
    [RequestSizeLimit(100_000_000)]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CloudItemDto>>> UploadFile(
        Guid id,
        IFormFile file,
        [FromForm] string? parentFolderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        if (file is null || file.Length <= 0)
        {
            return BadRequest(ApiResponse<CloudItemDto>.FromResult(false, "فایل برای آپلود الزامی است."));
        }

        await using var stream = file.OpenReadStream();
        var request = new UploadCloudFileRequest
        {
            Content = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            ParentFolderId = parentFolderId,
            ContentLength = file.Length
        };

        var result = await _cloudFileService.UploadFileAsync(_currentUserService.UserId, id, request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [Authorize]
    [HttpGet("accounts/{id:guid}/files/{itemId}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DownloadFile(
        Guid id,
        string itemId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _cloudFileService.DownloadFileAsync(_currentUserService.UserId, id, itemId, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return BadRequest(ApiResponse<object>.FromResult(false, result.Message, errors: result.Errors));
        }

        var download = result.Data;
        HttpContext.Response.RegisterForDispose(download);
        return File(download.Content, download.ContentType, download.FileName);
    }

    [Authorize]
    [HttpDelete("accounts/{id:guid}/items/{itemId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteItem(
        Guid id,
        string itemId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _cloudFileService.DeleteItemAsync(_currentUserService.UserId, id, itemId, cancellationToken);
        return this.ToApiResponse(result);
    }

    [Authorize]
    [HttpPost("accounts/{id:guid}/folders")]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CloudItemDto>>> CreateFolder(
        Guid id,
        [FromBody] CreateCloudFolderRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _cloudFileService.CreateFolderAsync(_currentUserService.UserId, id, request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [Authorize]
    [HttpPost("accounts/{id:guid}/items/{itemId}/move")]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CloudItemDto>>> MoveItem(
        Guid id,
        string itemId,
        [FromBody] MoveCloudItemRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _cloudFileService.MoveItemAsync(_currentUserService.UserId, id, itemId, request, cancellationToken);
        return this.ToApiResponse(result);
    }

    [Authorize]
    [HttpPatch("accounts/{id:guid}/items/{itemId}/rename")]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CloudItemDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CloudItemDto>>> RenameItem(
        Guid id,
        string itemId,
        [FromBody] RenameCloudItemRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return Unauthorized();
        }

        var result = await _cloudFileService.RenameItemAsync(_currentUserService.UserId, id, itemId, request, cancellationToken);
        return this.ToApiResponse(result);
    }

    private IActionResult HandleOAuthCallbackResult(
        Result<ConnectedCloudAccountDto> result,
        string? successRedirectUrl,
        string? failureRedirectUrl)
    {
        if (!string.IsNullOrWhiteSpace(successRedirectUrl) || !string.IsNullOrWhiteSpace(failureRedirectUrl))
        {
            if (result.Success
                && result.Data is not null
                && !string.IsNullOrWhiteSpace(successRedirectUrl))
            {
                var successUrl = QueryHelpers.AddQueryString(
                    successRedirectUrl,
                    new Dictionary<string, string?>
                    {
                        ["connected"] = "true",
                        ["accountId"] = result.Data.Id.ToString(),
                        ["provider"] = result.Data.Provider.ToString()
                    });

                return Redirect(successUrl);
            }

            if (!string.IsNullOrWhiteSpace(failureRedirectUrl))
            {
                var failureUrl = QueryHelpers.AddQueryString(
                    failureRedirectUrl,
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
}
