using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Models;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Infrastructure.Cloud.GoogleDrive;

public sealed partial class GoogleDriveProviderClient
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string FileFields = "id,name,mimeType,size,modifiedTime,parents,trashed";

    public async Task<CloudItemListResult> ListItemsAsync(
        string accessToken,
        ListCloudItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        var parentId = string.IsNullOrWhiteSpace(request.ParentId) ? "root" : request.ParentId;
        var queryParts = new List<string>
        {
            $"'{EscapeQueryValue(parentId)}' in parents",
            "trashed = false"
        };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            queryParts.Add($"name contains '{EscapeQueryValue(request.Search)}'");
        }

        if (request.IncludeFolders && !request.IncludeFiles)
        {
            queryParts.Add($"mimeType = '{FolderMimeType}'");
        }
        else if (!request.IncludeFolders && request.IncludeFiles)
        {
            queryParts.Add($"mimeType != '{FolderMimeType}'");
        }

        var query = new Dictionary<string, string?>
        {
            ["q"] = string.Join(" and ", queryParts),
            ["fields"] = $"nextPageToken,files({FileFields})",
            ["pageSize"] = Math.Clamp(request.PageSize, 1, 200).ToString(),
            ["orderBy"] = "folder,name",
            ["spaces"] = "drive",
            ["supportsAllDrives"] = "true",
            ["includeItemsFromAllDrives"] = "true"
        };

        if (!string.IsNullOrWhiteSpace(request.PageToken))
        {
            query["pageToken"] = request.PageToken;
        }

        var url = QueryHelpers.AddQueryString(_options.FilesEndpoint, query);
        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Get, url, accessToken);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "دریافت لیست فایل‌های گوگل درایو ناموفق بود.", cancellationToken);

        var payload = await DeserializeAsync<GoogleFileListApiResponse>(response, cancellationToken);
        var items = payload?.Files?
            .Where(x => x.Trashed != true)
            .Select(MapToCloudItem)
            .ToList()
            ?? [];

        return new CloudItemListResult
        {
            Items = items,
            NextPageToken = payload?.NextPageToken
        };
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

        var url = QueryHelpers.AddQueryString(
            $"{_options.FilesEndpoint}/{Uri.EscapeDataString(itemId)}",
            new Dictionary<string, string?>
            {
                ["fields"] = FileFields,
                ["supportsAllDrives"] = "true"
            });

        using var request = CreateAuthorizedRequest(HttpMethod.Get, url, accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("فایل یا پوشه مورد نظر یافت نشد.");
        }

        await EnsureSuccessAsync(response, "دریافت جزئیات آیتم گوگل درایو ناموفق بود.", cancellationToken);
        var file = await DeserializeAsync<GoogleFileApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("جزئیات آیتم گوگل درایو نامعتبر است.");

        if (file.Trashed == true)
        {
            throw new InvalidOperationException("فایل یا پوشه مورد نظر یافت نشد.");
        }

        return MapToCloudItem(file);
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

        var parentId = string.IsNullOrWhiteSpace(request.ParentFolderId) ? "root" : request.ParentFolderId;
        var metadata = new
        {
            name = request.FileName,
            parents = new[] { parentId }
        };

        var metadataJson = JsonSerializer.Serialize(metadata);
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType;

        using var multipart = new MultipartContent("related", "yekabr_boundary");
        multipart.Add(new StringContent(metadataJson, Encoding.UTF8, "application/json"));
        multipart.Add(new StreamContent(request.Content)
        {
            Headers =
            {
                ContentType = new MediaTypeHeaderValue(contentType)
            }
        });

        var url = QueryHelpers.AddQueryString(_options.UploadEndpoint, new Dictionary<string, string?>
        {
            ["uploadType"] = "multipart",
            ["fields"] = FileFields,
            ["supportsAllDrives"] = "true"
        });

        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, url, accessToken);
        httpRequest.Content = multipart;

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "آپلود فایل به گوگل درایو ناموفق بود.", cancellationToken);

        var file = await DeserializeAsync<GoogleFileApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("پاسخ آپلود گوگل درایو نامعتبر است.");

        return MapToCloudItem(file);
    }

    public async Task<CloudDownloadResult> DownloadFileAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(accessToken, itemId, cancellationToken);
        if (item.ItemType == CloudItemType.Folder)
        {
            throw new InvalidOperationException("دانلود پوشه از این مسیر مجاز نیست.");
        }

        HttpRequestMessage request;
        string fileName = item.Name;
        string contentType = item.MimeType ?? "application/octet-stream";

        if (IsGoogleWorkspaceMime(item.MimeType))
        {
            // Native Google Docs cannot use alt=media; export as PDF for downloadability.
            var exportUrl = QueryHelpers.AddQueryString(
                $"{_options.FilesEndpoint}/{Uri.EscapeDataString(itemId)}/export",
                new Dictionary<string, string?>
                {
                    ["mimeType"] = "application/pdf"
                });

            request = CreateAuthorizedRequest(HttpMethod.Get, exportUrl, accessToken);
            fileName = EnsureExtension(item.Name, ".pdf");
            contentType = "application/pdf";
        }
        else
        {
            var mediaUrl = QueryHelpers.AddQueryString(
                $"{_options.FilesEndpoint}/{Uri.EscapeDataString(itemId)}",
                new Dictionary<string, string?>
                {
                    ["alt"] = "media",
                    ["supportsAllDrives"] = "true"
                });

            request = CreateAuthorizedRequest(HttpMethod.Get, mediaUrl, accessToken);
        }

        var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            response.Dispose();
            request.Dispose();
            throw new InvalidOperationException("فایل مورد نظر یافت نشد.");
        }

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            request.Dispose();
            throw new InvalidOperationException("دانلود فایل از گوگل درایو ناموفق بود.");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var length = response.Content.Headers.ContentLength ?? item.Size;

        return new CloudDownloadResult(
            stream,
            fileName,
            response.Content.Headers.ContentType?.MediaType ?? contentType,
            length,
            new DownloadLifetime(request, response));
    }

    public async Task DeleteItemAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException("شناسه آیتم نامعتبر است.");
        }

        // Soft-delete via trash for safer MultCloud-like behavior.
        var url = QueryHelpers.AddQueryString(
            $"{_options.FilesEndpoint}/{Uri.EscapeDataString(itemId)}",
            new Dictionary<string, string?>
            {
                ["supportsAllDrives"] = "true"
            });

        using var request = CreateAuthorizedRequest(HttpMethod.Patch, url, accessToken);
        request.Content = JsonContent.Create(new { trashed = true });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("فایل یا پوشه مورد نظر یافت نشد.");
        }

        await EnsureSuccessAsync(response, "حذف آیتم در گوگل درایو ناموفق بود.", cancellationToken);
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

        var parentId = string.IsNullOrWhiteSpace(request.ParentFolderId) ? "root" : request.ParentFolderId;
        var url = QueryHelpers.AddQueryString(_options.FilesEndpoint, new Dictionary<string, string?>
        {
            ["fields"] = FileFields,
            ["supportsAllDrives"] = "true"
        });

        using var httpRequest = CreateAuthorizedRequest(HttpMethod.Post, url, accessToken);
        httpRequest.Content = JsonContent.Create(new
        {
            name = request.Name.Trim(),
            mimeType = FolderMimeType,
            parents = new[] { parentId }
        });

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "ایجاد پوشه در گوگل درایو ناموفق بود.", cancellationToken);

        var folder = await DeserializeAsync<GoogleFileApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("پاسخ ایجاد پوشه گوگل درایو نامعتبر است.");

        return MapToCloudItem(folder);
    }

    public async Task<CloudItem> MoveItemAsync(
        string accessToken,
        string itemId,
        string destinationParentFolderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(destinationParentFolderId))
        {
            throw new InvalidOperationException("شناسه مبدأ یا مقصد نامعتبر است.");
        }

        if (string.Equals(itemId, destinationParentFolderId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("انتقال یک پوشه به داخل خودش مجاز نیست.");
        }

        var current = await GetItemAsync(accessToken, itemId, cancellationToken);
        var previousParents = current.ParentId ?? "root";

        var url = QueryHelpers.AddQueryString(
            $"{_options.FilesEndpoint}/{Uri.EscapeDataString(itemId)}",
            new Dictionary<string, string?>
            {
                ["addParents"] = destinationParentFolderId,
                ["removeParents"] = previousParents,
                ["fields"] = FileFields,
                ["supportsAllDrives"] = "true"
            });

        using var request = CreateAuthorizedRequest(HttpMethod.Patch, url, accessToken);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "انتقال آیتم در گوگل درایو ناموفق بود.", cancellationToken);

        var moved = await DeserializeAsync<GoogleFileApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("پاسخ انتقال آیتم گوگل درایو نامعتبر است.");

        return MapToCloudItem(moved);
    }

    public async Task<CloudItem> RenameItemAsync(
        string accessToken,
        string itemId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException("شناسه آیتم نامعتبر است.");
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new InvalidOperationException("نام جدید الزامی است.");
        }

        var url = QueryHelpers.AddQueryString(
            $"{_options.FilesEndpoint}/{Uri.EscapeDataString(itemId)}",
            new Dictionary<string, string?>
            {
                ["fields"] = FileFields,
                ["supportsAllDrives"] = "true"
            });

        using var request = CreateAuthorizedRequest(HttpMethod.Patch, url, accessToken);
        request.Content = JsonContent.Create(new { name = newName.Trim() });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("فایل یا پوشه مورد نظر یافت نشد.");
        }

        await EnsureSuccessAsync(response, "تغییر نام آیتم در گوگل درایو ناموفق بود.", cancellationToken);

        var renamed = await DeserializeAsync<GoogleFileApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("پاسخ تغییر نام گوگل درایو نامعتبر است.");

        return MapToCloudItem(renamed);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static CloudItem MapToCloudItem(GoogleFileApiResponse file)
    {
        var isFolder = string.Equals(file.MimeType, FolderMimeType, StringComparison.OrdinalIgnoreCase);

        return new CloudItem
        {
            Id = file.Id ?? string.Empty,
            Name = file.Name ?? string.Empty,
            FullPath = null,
            ItemType = isFolder ? CloudItemType.Folder : CloudItemType.File,
            Size = ParseLong(file.Size),
            MimeType = file.MimeType,
            ModifiedAtUtc = file.ModifiedTime?.UtcDateTime,
            ParentId = file.Parents?.FirstOrDefault(),
            CanDownload = !isFolder,
            CanDelete = true,
            CanMove = true
        };
    }

    private static bool IsGoogleWorkspaceMime(string? mimeType)
    {
        return !string.IsNullOrWhiteSpace(mimeType)
               && mimeType.StartsWith("application/vnd.google-apps.", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(mimeType, FolderMimeType, StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeQueryValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
    }

    private static string EnsureExtension(string fileName, string extension)
    {
        return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + extension;
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

    private sealed class GoogleFileListApiResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }

        [JsonPropertyName("files")]
        public List<GoogleFileApiResponse>? Files { get; set; }
    }

    private sealed class GoogleFileApiResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }

        [JsonPropertyName("size")]
        public string? Size { get; set; }

        [JsonPropertyName("modifiedTime")]
        public DateTimeOffset? ModifiedTime { get; set; }

        [JsonPropertyName("parents")]
        public List<string>? Parents { get; set; }

        [JsonPropertyName("trashed")]
        public bool? Trashed { get; set; }
    }
}
