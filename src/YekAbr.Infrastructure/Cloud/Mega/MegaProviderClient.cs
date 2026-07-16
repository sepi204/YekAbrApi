using System.Text.Json;
using CG.Web.MegaApiClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Models;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Infrastructure.Cloud.Mega;

public sealed class MegaProviderClient : CloudProviderClientBase, IMegaProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly MegaOptions _options;
    private readonly ILogger<MegaProviderClient> _logger;

    public MegaProviderClient(IOptions<MegaOptions> options, ILogger<MegaProviderClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public override CloudProviderType ProviderType => CloudProviderType.Mega;

    public override string ProviderName => "MEGA";

    public async Task<MegaConnectionMaterial> CreateConnectionMaterialAsync(
        string email,
        string password,
        string? mfaKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("ایمیل و رمز عبور حساب مگا الزامی است.");
        }

        var client = CreateClient();
        try
        {
            var authInfos = await client.GenerateAuthInfosAsync(
                email.Trim(),
                password,
                string.IsNullOrWhiteSpace(mfaKey) ? null : mfaKey.Trim());

            cancellationToken.ThrowIfCancellationRequested();
            await client.LoginAsync(authInfos);

            var nodes = (await client.GetNodesAsync()).ToList();
            var root = nodes.FirstOrDefault(n => n.Type == NodeType.Root)
                ?? throw new InvalidOperationException("پوشه ریشه حساب مگا یافت نشد.");

            var normalizedEmail = email.Trim().ToLowerInvariant();
            return new MegaConnectionMaterial
            {
                AccountInfo = new CloudProviderAccountInfo
                {
                    ProviderAccountId = normalizedEmail,
                    Email = email.Trim(),
                    DisplayName = email.Trim()
                },
                AuthInfosJson = SerializeAuthInfos(authInfos),
                RootFolderId = root.Id
            };
        }
        catch (ApiException exception)
        {
            _logger.LogWarning(exception, "MEGA authentication failed.");
            throw new InvalidOperationException(MapApiException(exception, "احراز هویت مگا ناموفق بود."), exception);
        }
        finally
        {
            SafeLogout(client);
        }
    }

    public async Task<CloudStorageUsage> GetStorageUsageAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        return await WithSessionAsync(accessToken, async client =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var account = await client.GetAccountInformationAsync();
            var total = account.TotalQuota;
            var used = account.UsedQuota;
            var free = Math.Max(0, total - used);

            return new CloudStorageUsage
            {
                TotalBytes = total,
                UsedBytes = used,
                FreeBytes = free
            };
        }, cancellationToken);
    }

    public async Task<CloudItemListResult> ListItemsAsync(
        string accessToken,
        ListCloudItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await WithSessionAsync(accessToken, async client =>
        {
            var nodes = (await client.GetNodesAsync()).ToList();
            var parent = ResolveParentNode(nodes, request.ParentId);

            var children = nodes
                .Where(n => n.ParentId == parent.Id)
                .Where(n => n.Type is NodeType.Directory or NodeType.File)
                .Select(n => MapNode(n, parent.Id, nodes))
                .Where(x =>
                    (request.IncludeFolders || x.ItemType != CloudItemType.Folder)
                    && (request.IncludeFiles || x.ItemType != CloudItemType.File))
                .Where(x => string.IsNullOrWhiteSpace(request.Search)
                            || x.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.ItemType == CloudItemType.Folder ? 0 : 1)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // MEGA has no native cursor pagination; support simple offset tokens.
            var offset = ParseOffset(request.PageToken);
            var pageSize = Math.Clamp(request.PageSize <= 0 ? 50 : request.PageSize, 1, 500);
            var page = children.Skip(offset).Take(pageSize).ToList();
            var nextOffset = offset + page.Count;
            var nextToken = nextOffset < children.Count ? nextOffset.ToString() : null;

            return new CloudItemListResult
            {
                Items = page,
                NextPageToken = nextToken
            };
        }, cancellationToken);
    }

    public async Task<CloudItem> GetItemAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        return await WithSessionAsync(accessToken, async client =>
        {
            var nodes = (await client.GetNodesAsync()).ToList();
            var node = FindNode(nodes, itemId);
            return MapNode(node, node.ParentId, nodes);
        }, cancellationToken);
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

        return await WithSessionAsync(accessToken, async client =>
        {
            var nodes = (await client.GetNodesAsync()).ToList();
            var parent = ResolveParentNode(nodes, request.ParentFolderId);

            cancellationToken.ThrowIfCancellationRequested();
            var uploaded = await client.UploadAsync(
                request.Content,
                request.FileName.Trim(),
                parent,
                progress: null,
                modificationDate: null,
                cancellationToken: cancellationToken);

            return MapNode(uploaded, parent.Id, nodes);
        }, cancellationToken);
    }

    public async Task<CloudDownloadResult> DownloadFileAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        try
        {
            await LoginAsync(client, accessToken, cancellationToken);
            var nodes = (await client.GetNodesAsync()).ToList();
            var node = FindNode(nodes, itemId);

            if (node.Type != NodeType.File)
            {
                SafeLogout(client);
                throw new InvalidOperationException("دانلود پوشه از این مسیر مجاز نیست.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var stream = await client.DownloadAsync(node, progress: null, cancellationToken: cancellationToken);
            var fileName = string.IsNullOrWhiteSpace(node.Name) ? "download.bin" : node.Name;
            var contentType = GuessContentType(fileName);

            // Keep MEGA session alive until the download stream is disposed by the caller.
            return new CloudDownloadResult(
                stream,
                fileName,
                contentType,
                node.Size,
                new MegaSessionLifetime(client));
        }
        catch (ApiException exception)
        {
            SafeLogout(client);
            _logger.LogWarning(exception, "MEGA download failed.");
            throw new InvalidOperationException(MapApiException(exception, "دانلود فایل از مگا ناموفق بود."), exception);
        }
        catch
        {
            SafeLogout(client);
            throw;
        }
    }

    public async Task DeleteItemAsync(
        string accessToken,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        await WithSessionAsync(accessToken, async client =>
        {
            var nodes = (await client.GetNodesAsync()).ToList();
            var node = FindNode(nodes, itemId);
            if (node.Type is NodeType.Root or NodeType.Inbox or NodeType.Trash)
            {
                throw new InvalidOperationException("حذف این آیتم سیستمی مجاز نیست.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await client.DeleteAsync(node, moveToTrash: true);
            return true;
        }, cancellationToken);
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

        return await WithSessionAsync(accessToken, async client =>
        {
            var nodes = (await client.GetNodesAsync()).ToList();
            var parent = ResolveParentNode(nodes, request.ParentFolderId);

            cancellationToken.ThrowIfCancellationRequested();
            var folder = await client.CreateFolderAsync(request.Name.Trim(), parent);
            return MapNode(folder, parent.Id, nodes);
        }, cancellationToken);
    }

    public async Task<CloudItem> MoveItemAsync(
        string accessToken,
        string itemId,
        string destinationParentFolderId,
        CancellationToken cancellationToken = default)
    {
        return await WithSessionAsync(accessToken, async client =>
        {
            var nodes = (await client.GetNodesAsync()).ToList();
            var node = FindNode(nodes, itemId);
            var destination = ResolveParentNode(nodes, destinationParentFolderId);

            if (node.Type is NodeType.Root or NodeType.Inbox or NodeType.Trash)
            {
                throw new InvalidOperationException("انتقال این آیتم سیستمی مجاز نیست.");
            }

            if (destination.Id == node.Id)
            {
                throw new InvalidOperationException("مقصد انتقال نامعتبر است.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var moved = await client.MoveAsync(node, destination);
            return MapNode(moved, destination.Id, nodes);
        }, cancellationToken);
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

        return await WithSessionAsync(accessToken, async client =>
        {
            var nodes = (await client.GetNodesAsync()).ToList();
            var node = FindNode(nodes, itemId);

            if (node.Type is NodeType.Root or NodeType.Inbox or NodeType.Trash)
            {
                throw new InvalidOperationException("تغییر نام این آیتم سیستمی مجاز نیست.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var renamed = await client.RenameAsync(node, newName.Trim());
            return MapNode(renamed, renamed.ParentId, nodes);
        }, cancellationToken);
    }

    private async Task<T> WithSessionAsync<T>(
        string authInfosJson,
        Func<MegaApiClient, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var client = CreateClient();
        try
        {
            await LoginAsync(client, authInfosJson, cancellationToken);
            return await action(client);
        }
        catch (ApiException exception)
        {
            _logger.LogWarning(exception, "MEGA provider operation failed.");
            throw new InvalidOperationException(MapApiException(exception, "عملیات مگا ناموفق بود."), exception);
        }
        finally
        {
            SafeLogout(client);
        }
    }

    private MegaApiClient CreateClient()
    {
        // MegaApiClient uses its own transport defaults; RequestTimeoutSeconds is reserved for future Options wiring.
        _ = _options;
        return new MegaApiClient();
    }

    private async Task LoginAsync(MegaApiClient client, string authInfosJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authInfosJson))
        {
            throw new InvalidOperationException("اعتبارنامه مگا موجود نیست. لطفاً دوباره متصل شوید.");
        }

        var authInfos = DeserializeAuthInfos(authInfosJson);
        cancellationToken.ThrowIfCancellationRequested();
        await client.LoginAsync(authInfos);
    }

    private static string SerializeAuthInfos(MegaApiClient.AuthInfos authInfos)
    {
        var payload = new MegaAuthInfosPayload
        {
            Email = authInfos.Email,
            Hash = authInfos.Hash,
            PasswordAesKey = authInfos.PasswordAesKey,
            MfaKey = authInfos.MFAKey
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static MegaApiClient.AuthInfos DeserializeAuthInfos(string json)
    {
        MegaAuthInfosPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<MegaAuthInfosPayload>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("اعتبارنامه مگا نامعتبر است. لطفاً دوباره متصل شوید.", exception);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.Email)
            || string.IsNullOrWhiteSpace(payload.Hash)
            || payload.PasswordAesKey is null
            || payload.PasswordAesKey.Length == 0)
        {
            throw new InvalidOperationException("اعتبارنامه مگا نامعتبر است. لطفاً دوباره متصل شوید.");
        }

        return new MegaApiClient.AuthInfos(
            payload.Email,
            payload.Hash,
            payload.PasswordAesKey,
            payload.MfaKey);
    }

    private static INode ResolveParentNode(IReadOnlyList<INode> nodes, string? parentId)
    {
        if (string.IsNullOrWhiteSpace(parentId)
            || string.Equals(parentId, "root", StringComparison.OrdinalIgnoreCase))
        {
            return nodes.FirstOrDefault(n => n.Type == NodeType.Root)
                ?? throw new InvalidOperationException("پوشه ریشه حساب مگا یافت نشد.");
        }

        var node = FindNode(nodes, parentId);
        if (node.Type is not (NodeType.Directory or NodeType.Root))
        {
            throw new InvalidOperationException("پوشه والد نامعتبر است.");
        }

        return node;
    }

    private static INode FindNode(IReadOnlyList<INode> nodes, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException("شناسه آیتم نامعتبر است.");
        }

        var node = nodes.FirstOrDefault(n => string.Equals(n.Id, itemId, StringComparison.Ordinal));
        if (node is null)
        {
            throw new InvalidOperationException("فایل یا پوشه مورد نظر یافت نشد.");
        }

        return node;
    }

    private static CloudItem MapNode(INode node, string? parentId, IReadOnlyList<INode> allNodes)
    {
        var isFolder = node.Type is NodeType.Directory or NodeType.Root;
        return new CloudItem
        {
            Id = node.Id,
            Name = node.Name ?? (node.Type == NodeType.Root ? "Root" : string.Empty),
            FullPath = BuildPath(node, allNodes),
            ItemType = isFolder ? CloudItemType.Folder : CloudItemType.File,
            Size = isFolder ? null : node.Size,
            MimeType = isFolder
                ? "application/vnd.mega.folder"
                : GuessContentType(node.Name),
            ModifiedAtUtc = node.ModificationDate?.ToUniversalTime()
                ?? node.CreationDate?.ToUniversalTime(),
            ParentId = parentId,
            CanDownload = !isFolder,
            CanDelete = node.Type is NodeType.File or NodeType.Directory,
            CanMove = node.Type is NodeType.File or NodeType.Directory
        };
    }

    private static string? BuildPath(INode node, IReadOnlyList<INode> allNodes)
    {
        if (node.Type == NodeType.Root)
        {
            return "/";
        }

        var segments = new Stack<string>();
        var current = node;
        var guard = 0;
        while (current is not null && current.Type != NodeType.Root && guard++ < 256)
        {
            if (!string.IsNullOrWhiteSpace(current.Name))
            {
                segments.Push(current.Name);
            }

            if (string.IsNullOrWhiteSpace(current.ParentId))
            {
                break;
            }

            current = allNodes.FirstOrDefault(n => n.Id == current.ParentId);
        }

        return segments.Count == 0 ? null : "/" + string.Join('/', segments);
    }

    private static int ParseOffset(string? pageToken)
    {
        if (string.IsNullOrWhiteSpace(pageToken))
        {
            return 0;
        }

        return int.TryParse(pageToken, out var offset) && offset >= 0 ? offset : 0;
    }

    private static string GuessContentType(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "application/octet-stream";
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".zip" => "application/zip",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream"
        };
    }

    private static string MapApiException(ApiException exception, string fallback)
    {
        return exception.ApiResultCode switch
        {
            ApiResultCode.BadArguments => "پارامترهای درخواست مگا نامعتبر است.",
            ApiResultCode.RequestFailedRetry => "سرویس مگا موقتاً در دسترس نیست. لطفاً دوباره تلاش کنید.",
            ApiResultCode.TooManyRequests or ApiResultCode.ToManyRequestsForThisResource =>
                "تعداد درخواست‌های مگا بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید.",
            ApiResultCode.AccessDenied => "دسترسی به منبع مگا مجاز نیست.",
            ApiResultCode.ResourceNotExists => "فایل یا پوشه مورد نظر در مگا یافت نشد.",
            ApiResultCode.QuotaExceeded => "سهمیه فضای ذخیره‌سازی مگا تکمیل شده است.",
            ApiResultCode.ResourceAlreadyExists => "آیتم با این نام از قبل در مگا وجود دارد.",
            ApiResultCode.RequestIncomplete => "درخواست مگا ناقص است.",
            ApiResultCode.CryptographicError => "خطای رمزنگاری در ارتباط با مگا رخ داد.",
            ApiResultCode.BadSessionId => "نشست مگا نامعتبر است. لطفاً دوباره متصل شوید.",
            ApiResultCode.ResourceAdministrativelyBlocked => "حساب مگا مسدود شده است.",
            ApiResultCode.TwoFactorAuthenticationError => "کد احراز هویت دو مرحله‌ای مگا نامعتبر است.",
            ApiResultCode.CircularLinkage => "مقصد انتقال مگا باعث ایجاد حلقه نامعتبر می‌شود.",
            _ => fallback
        };
    }

    private static void SafeLogout(MegaApiClient client)
    {
        try
        {
            if (client.IsLoggedIn)
            {
                client.Logout();
            }
        }
        catch
        {
            // Ignore logout failures during cleanup.
        }
    }

    private sealed class MegaSessionLifetime : IDisposable
    {
        private MegaApiClient? _client;

        public MegaSessionLifetime(MegaApiClient client)
        {
            _client = client;
        }

        public void Dispose()
        {
            var client = Interlocked.Exchange(ref _client, null);
            if (client is not null)
            {
                SafeLogout(client);
            }
        }
    }
}
