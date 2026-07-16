using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Models;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Infrastructure.Cloud.Dropbox;

public sealed class DropboxProviderClient : CloudProviderClientBase, IDropboxProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly DropboxOptions _options;

    public DropboxProviderClient(HttpClient httpClient, IOptions<DropboxOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public override CloudProviderType ProviderType => CloudProviderType.Dropbox;

    public override string ProviderName => "Dropbox";

    public string BuildAuthorizationUrl(string state)
    {
        EnsureConfigured();

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["token_access_type"] = "offline",
            ["state"] = state
        };

        if (!string.IsNullOrWhiteSpace(_options.Scopes))
        {
            query["scope"] = _options.Scopes;
        }

        return QueryHelpers.AddQueryString(_options.AuthorizationEndpoint, query);
    }

    public async Task<CloudOAuthTokenResult> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("کد مجوز دراپ‌باکس نامعتبر است.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri
        });

        using var response = await _httpClient.PostAsync(_options.TokenEndpoint, content, cancellationToken);
        await EnsureSuccessAsync(response, "تبادل کد مجوز دراپ‌باکس ناموفق بود.", cancellationToken);

        var token = await DeserializeAsync<DropboxTokenApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("پاسخ توکن دراپ‌باکس نامعتبر است.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("توکن دسترسی دراپ‌باکس دریافت نشد.");
        }

        return MapTokenResult(token);
    }

    public async Task<CloudOAuthTokenResult> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("توکن تازه‌سازی دراپ‌باکس موجود نیست.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        });

        using var response = await _httpClient.PostAsync(_options.TokenEndpoint, content, cancellationToken);
        await EnsureSuccessAsync(response, "تازه‌سازی توکن دسترسی دراپ‌باکس ناموفق بود.", cancellationToken);

        var token = await DeserializeAsync<DropboxTokenApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("پاسخ تازه‌سازی توکن دراپ‌باکس نامعتبر است.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("توکن دسترسی جدید دراپ‌باکس دریافت نشد.");
        }

        token.RefreshToken ??= refreshToken;
        return MapTokenResult(token);
    }

    public async Task<CloudProviderAccountInfo> GetAccountInfoAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ApiEndpoint}/users/get_current_account", accessToken);
        request.Content = new StringContent("null", Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "دریافت اطلاعات حساب دراپ‌باکس ناموفق بود.", cancellationToken);

        var account = await DeserializeAsync<DropboxAccountApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("اطلاعات حساب دراپ‌باکس نامعتبر است.");

        if (string.IsNullOrWhiteSpace(account.AccountId))
        {
            throw new InvalidOperationException("شناسه حساب دراپ‌باکس دریافت نشد.");
        }

        var displayName = account.Name?.DisplayName
            ?? account.Name?.FamiliarName
            ?? account.Email
            ?? account.AccountId;

        return new CloudProviderAccountInfo
        {
            ProviderAccountId = account.AccountId,
            Email = account.Email ?? string.Empty,
            DisplayName = displayName
        };
    }

    public async Task<CloudStorageUsage> GetStorageUsageAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ApiEndpoint}/users/get_space_usage", accessToken);
        request.Content = new StringContent("null", Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "دریافت فضای ذخیره‌سازی دراپ‌باکس ناموفق بود.", cancellationToken);

        var usage = await DeserializeAsync<DropboxSpaceUsageApiResponse>(response, cancellationToken);
        var used = usage?.Used;
        long? total = usage?.Allocation?.Allocated;
        long? free = total.HasValue && used.HasValue
            ? Math.Max(0, total.Value - used.Value)
            : null;

        return new CloudStorageUsage
        {
            TotalBytes = total,
            UsedBytes = used,
            FreeBytes = free
        };
    }

    public async Task<CloudItemListResult> ListItemsAsync(
        string accessToken,
        ListCloudItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        var parentPathOrId = NormalizeRoot(request.ParentId);

        if (!string.IsNullOrWhiteSpace(request.PageToken))
        {
            return await ListContinueAsync(accessToken, request.PageToken, request, parentPathOrId, cancellationToken);
        }

        var body = new
        {
            path = await ResolvePathAsync(accessToken, parentPathOrId, cancellationToken),
            recursive = false,
            include_media_info = false,
            include_deleted = false,
            limit = Math.Clamp(request.PageSize, 1, 200)
        };

        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ApiEndpoint}/files/list_folder", accessToken);
        httpRequest.Content = CreateJsonContent(body);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "دریافت لیست فایل‌های دراپ‌باکس ناموفق بود.", cancellationToken);

        var payload = await DeserializeAsync<DropboxListFolderApiResponse>(response, cancellationToken);
        return MapListResult(payload, request, parentPathOrId);
    }

    public async Task<CloudItem> GetItemAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException("شناسه آیتم نامعتبر است.");
        }

        var metadata = await GetMetadataAsync(accessToken, NormalizeRoot(itemId), cancellationToken);
        return MapEntry(metadata, GuessParentId(metadata.PathDisplay));
    }

    public async Task<CloudItem> UploadFileAsync(
        string accessToken,
        UploadCloudFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new InvalidOperationException("نام فایل الزامی است.");
        }

        var parentPath = await ResolvePathAsync(accessToken, NormalizeRoot(request.ParentFolderId), cancellationToken);
        var targetPath = CombinePath(parentPath, request.FileName);

        var dropboxArg = JsonSerializer.Serialize(new
        {
            path = targetPath,
            mode = "add",
            autorename = true,
            mute = false,
            strict_conflict = false
        }, JsonOptions);

        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ContentEndpoint}/files/upload", accessToken);
        httpRequest.Headers.Add("Dropbox-API-Arg", dropboxArg);
        httpRequest.Content = new StreamContent(request.Content);
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "آپلود فایل به دراپ‌باکس ناموفق بود.", cancellationToken);

        var metadata = await DeserializeAsync<DropboxMetadataApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("پاسخ آپلود دراپ‌باکس نامعتبر است.");

        return MapEntry(metadata, NormalizeRoot(request.ParentFolderId));
    }

    public async Task<CloudDownloadResult> DownloadFileAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetMetadataAsync(accessToken, NormalizeRoot(itemId), cancellationToken);
        if (IsFolder(metadata))
        {
            throw new InvalidOperationException("دانلود پوشه از این مسیر مجاز نیست.");
        }

        var pathOrId = !string.IsNullOrWhiteSpace(metadata.Id)
            ? metadata.Id
            : metadata.PathDisplay ?? itemId;

        var dropboxArg = JsonSerializer.Serialize(new { path = pathOrId }, JsonOptions);
        var request = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ContentEndpoint}/files/download", accessToken);
        request.Headers.Add("Dropbox-API-Arg", dropboxArg);
        request.Content = new StringContent(string.Empty);

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            request.Dispose();
            throw new InvalidOperationException("دانلود فایل از دراپ‌باکس ناموفق بود.");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var fileName = metadata.Name ?? Path.GetFileName(metadata.PathDisplay ?? "download.bin");
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

        return new CloudDownloadResult(
            stream,
            fileName,
            contentType,
            response.Content.Headers.ContentLength ?? metadata.Size,
            new DownloadLifetime(request, response));
    }

    public async Task DeleteItemAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        var pathOrId = await ResolvePathOrIdAsync(accessToken, NormalizeRoot(itemId), cancellationToken);
        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ApiEndpoint}/files/delete_v2", accessToken);
        request.Content = CreateJsonContent(new { path = pathOrId });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "حذف آیتم در دراپ‌باکس ناموفق بود.", cancellationToken);
    }

    public async Task<CloudItem> CreateFolderAsync(
        string accessToken,
        CreateCloudFolderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("نام پوشه الزامی است.");
        }

        var parentPath = await ResolvePathAsync(accessToken, NormalizeRoot(request.ParentFolderId), cancellationToken);
        var folderPath = CombinePath(parentPath, request.Name.Trim());

        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ApiEndpoint}/files/create_folder_v2", accessToken);
        httpRequest.Content = CreateJsonContent(new { path = folderPath, autorename = false });

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "ایجاد پوشه در دراپ‌باکس ناموفق بود.", cancellationToken);

        var payload = await DeserializeAsync<DropboxCreateFolderApiResponse>(response, cancellationToken);
        var metadata = payload?.Metadata
            ?? throw new InvalidOperationException("پاسخ ایجاد پوشه دراپ‌باکس نامعتبر است.");

        return MapEntry(metadata, NormalizeRoot(request.ParentFolderId));
    }

    public async Task<CloudItem> MoveItemAsync(
        string accessToken,
        string itemId,
        string destinationParentFolderId,
        CancellationToken cancellationToken = default)
    {
        var source = await GetMetadataAsync(accessToken, NormalizeRoot(itemId), cancellationToken);
        var destinationParentPath = await ResolvePathAsync(accessToken, NormalizeRoot(destinationParentFolderId), cancellationToken);
        var toPath = CombinePath(destinationParentPath, source.Name ?? "item");

        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ApiEndpoint}/files/move_v2", accessToken);
        request.Content = CreateJsonContent(new
        {
            from_path = source.Id ?? source.PathDisplay,
            to_path = toPath,
            autorename = false
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "انتقال آیتم در دراپ‌باکس ناموفق بود.", cancellationToken);

        var payload = await DeserializeAsync<DropboxRelocationApiResponse>(response, cancellationToken);
        var metadata = payload?.Metadata
            ?? throw new InvalidOperationException("پاسخ انتقال آیتم دراپ‌باکس نامعتبر است.");

        return MapEntry(metadata, NormalizeRoot(destinationParentFolderId));
    }

    public async Task<CloudItem> RenameItemAsync(
        string accessToken,
        string itemId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new InvalidOperationException("نام جدید الزامی است.");
        }

        var source = await GetMetadataAsync(accessToken, NormalizeRoot(itemId), cancellationToken);
        var parentPath = GetParentPath(source.PathDisplay);
        var toPath = CombinePath(parentPath, newName.Trim());

        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ApiEndpoint}/files/move_v2", accessToken);
        request.Content = CreateJsonContent(new
        {
            from_path = source.Id ?? source.PathDisplay,
            to_path = toPath,
            autorename = false
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "تغییر نام آیتم در دراپ‌باکس ناموفق بود.", cancellationToken);

        var payload = await DeserializeAsync<DropboxRelocationApiResponse>(response, cancellationToken);
        var metadata = payload?.Metadata
            ?? throw new InvalidOperationException("پاسخ تغییر نام دراپ‌باکس نامعتبر است.");

        return MapEntry(metadata, GuessParentId(metadata.PathDisplay));
    }

    private async Task<CloudItemListResult> ListContinueAsync(
        string accessToken,
        string cursor,
        ListCloudItemsRequest request,
        string parentId,
        CancellationToken cancellationToken)
    {
        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ApiEndpoint}/files/list_folder/continue", accessToken);
        httpRequest.Content = CreateJsonContent(new { cursor });

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "ادامه دریافت لیست فایل‌های دراپ‌باکس ناموفق بود.", cancellationToken);

        var payload = await DeserializeAsync<DropboxListFolderApiResponse>(response, cancellationToken);
        return MapListResult(payload, request, parentId);
    }

    private CloudItemListResult MapListResult(
        DropboxListFolderApiResponse? payload,
        ListCloudItemsRequest request,
        string parentId)
    {
        var items = payload?.Entries?
            .Select(x => MapEntry(x, parentId))
            .Where(x =>
                (request.IncludeFolders || x.ItemType != CloudItemType.Folder)
                && (request.IncludeFiles || x.ItemType != CloudItemType.File))
            .Where(x => string.IsNullOrWhiteSpace(request.Search)
                        || x.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase))
            .ToList()
            ?? [];

        return new CloudItemListResult
        {
            Items = items,
            NextPageToken = payload?.HasMore == true ? payload.Cursor : null
        };
    }

    private async Task<DropboxMetadataApiResponse> GetMetadataAsync(
        string accessToken,
        string pathOrId,
        CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{_options.ApiEndpoint}/files/get_metadata", accessToken);
        request.Content = CreateJsonContent(new
        {
            path = pathOrId,
            include_deleted = false
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode == 409)
        {
            throw new InvalidOperationException("فایل یا پوشه مورد نظر یافت نشد.");
        }

        await EnsureSuccessAsync(response, "دریافت جزئیات آیتم دراپ‌باکس ناموفق بود.", cancellationToken);

        return await DeserializeAsync<DropboxMetadataApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("جزئیات آیتم دراپ‌باکس نامعتبر است.");
    }

    private async Task<string> ResolvePathAsync(string accessToken, string pathOrId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(pathOrId))
        {
            return string.Empty;
        }

        if (pathOrId.StartsWith('/'))
        {
            return pathOrId;
        }

        if (pathOrId.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
        {
            var metadata = await GetMetadataAsync(accessToken, pathOrId, cancellationToken);
            return metadata.PathDisplay ?? pathOrId;
        }

        return pathOrId.StartsWith('/') ? pathOrId : "/" + pathOrId.TrimStart('/');
    }

    private async Task<string> ResolvePathOrIdAsync(string accessToken, string pathOrId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(pathOrId))
        {
            throw new InvalidOperationException("شناسه آیتم نامعتبر است.");
        }

        if (pathOrId.StartsWith("id:", StringComparison.OrdinalIgnoreCase) || pathOrId.StartsWith('/'))
        {
            return pathOrId;
        }

        return await ResolvePathAsync(accessToken, pathOrId, cancellationToken);
    }

    private static CloudItem MapEntry(DropboxMetadataApiResponse entry, string? parentId)
    {
        var isFolder = IsFolder(entry);
        return new CloudItem
        {
            Id = entry.Id ?? entry.PathDisplay ?? string.Empty,
            Name = entry.Name ?? Path.GetFileName(entry.PathDisplay ?? string.Empty),
            FullPath = entry.PathDisplay,
            ItemType = isFolder ? CloudItemType.Folder : CloudItemType.File,
            Size = isFolder ? null : entry.Size,
            MimeType = isFolder ? "application/vnd.dropbox.folder" : null,
            ModifiedAtUtc = entry.ClientModified?.UtcDateTime ?? entry.ServerModified?.UtcDateTime,
            ParentId = parentId,
            CanDownload = !isFolder,
            CanDelete = true,
            CanMove = true
        };
    }

    private static bool IsFolder(DropboxMetadataApiResponse entry)
    {
        return string.Equals(entry.Tag, "folder", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoot(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "root", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return value;
    }

    private static string CombinePath(string parentPath, string name)
    {
        if (string.IsNullOrEmpty(parentPath))
        {
            return "/" + name.Trim('/');
        }

        return parentPath.TrimEnd('/') + "/" + name.Trim('/');
    }

    private static string GetParentPath(string? pathDisplay)
    {
        if (string.IsNullOrWhiteSpace(pathDisplay))
        {
            return string.Empty;
        }

        var trimmed = pathDisplay.TrimEnd('/');
        var index = trimmed.LastIndexOf('/');
        if (index <= 0)
        {
            return string.Empty;
        }

        return trimmed[..index];
    }

    private static string? GuessParentId(string? pathDisplay)
    {
        var parentPath = GetParentPath(pathDisplay);
        return string.IsNullOrEmpty(parentPath) ? string.Empty : parentPath;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret)
            || string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            throw new InvalidOperationException("پیکربندی دراپ‌باکس ناقص است.");
        }
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static StringContent CreateJsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            _ = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(failureMessage);
        }
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static CloudOAuthTokenResult MapTokenResult(DropboxTokenApiResponse token)
    {
        DateTime? expiresAt = token.ExpiresIn is > 0
            ? DateTime.UtcNow.AddSeconds(token.ExpiresIn.Value)
            : null;

        return new CloudOAuthTokenResult
        {
            AccessToken = token.AccessToken!,
            RefreshToken = token.RefreshToken,
            AccessTokenExpiresAtUtc = expiresAt,
            TokenType = token.TokenType,
            Scope = token.Scope
        };
    }

    private sealed class DownloadLifetime : IDisposable
    {
        private readonly HttpRequestMessage _request;
        private readonly HttpResponseMessage _response;

        public DownloadLifetime(HttpRequestMessage request, HttpResponseMessage response)
        {
            _request = request;
            _response = response;
        }

        public void Dispose()
        {
            _response.Dispose();
            _request.Dispose();
        }
    }

    private sealed class DropboxTokenApiResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    private sealed class DropboxAccountApiResponse
    {
        [JsonPropertyName("account_id")]
        public string? AccountId { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        public DropboxNameApiResponse? Name { get; set; }
    }

    private sealed class DropboxNameApiResponse
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("familiar_name")]
        public string? FamiliarName { get; set; }
    }

    private sealed class DropboxSpaceUsageApiResponse
    {
        [JsonPropertyName("used")]
        public long? Used { get; set; }

        [JsonPropertyName("allocation")]
        public DropboxAllocationApiResponse? Allocation { get; set; }
    }

    private sealed class DropboxAllocationApiResponse
    {
        [JsonPropertyName("allocated")]
        public long? Allocated { get; set; }
    }

    private sealed class DropboxListFolderApiResponse
    {
        [JsonPropertyName("entries")]
        public List<DropboxMetadataApiResponse>? Entries { get; set; }

        [JsonPropertyName("cursor")]
        public string? Cursor { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }
    }

    private sealed class DropboxMetadataApiResponse
    {
        [JsonPropertyName(".tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("path_display")]
        public string? PathDisplay { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("client_modified")]
        public DateTimeOffset? ClientModified { get; set; }

        [JsonPropertyName("server_modified")]
        public DateTimeOffset? ServerModified { get; set; }
    }

    private sealed class DropboxCreateFolderApiResponse
    {
        [JsonPropertyName("metadata")]
        public DropboxMetadataApiResponse? Metadata { get; set; }
    }

    private sealed class DropboxRelocationApiResponse
    {
        [JsonPropertyName("metadata")]
        public DropboxMetadataApiResponse? Metadata { get; set; }
    }
}
