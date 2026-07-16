using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using YekAbr.Domain.Enums;
using YekAbr.Domain.Models;
using YekAbr.Services.DTOs.Cloud;
using YekAbr.Services.Interfaces.Cloud;

namespace YekAbr.Infrastructure.Cloud.GoogleDrive;

public sealed partial class GoogleDriveProviderClient : CloudProviderClientBase, IGoogleDriveProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly GoogleDriveOptions _options;

    public GoogleDriveProviderClient(HttpClient httpClient, IOptions<GoogleDriveOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public override CloudProviderType ProviderType => CloudProviderType.GoogleDrive;

    public override string ProviderName => "Google Drive";

    public string BuildAuthorizationUrl(string state)
    {
        EnsureConfigured();

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', _options.Scopes),
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = state
        };

        return QueryHelpers.AddQueryString(_options.AuthorizationEndpoint, query);
    }

    public async Task<CloudOAuthTokenResult> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("کد مجوز گوگل نامعتبر است.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await _httpClient.PostAsync(_options.TokenEndpoint, content, cancellationToken);
        await EnsureSuccessAsync(response, "تبادل کد مجوز گوگل ناموفق بود.", cancellationToken);

        var tokenResponse = await DeserializeAsync<GoogleTokenApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("پاسخ توکن گوگل نامعتبر است.");

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("توکن دسترسی گوگل دریافت نشد.");
        }

        return MapTokenResult(tokenResponse);
    }

    public async Task<CloudOAuthTokenResult> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("توکن تازه‌سازی گوگل موجود نیست.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });

        using var response = await _httpClient.PostAsync(_options.TokenEndpoint, content, cancellationToken);
        await EnsureSuccessAsync(response, "تازه‌سازی توکن دسترسی گوگل ناموفق بود.", cancellationToken);

        var tokenResponse = await DeserializeAsync<GoogleTokenApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("پاسخ تازه‌سازی توکن گوگل نامعتبر است.");

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("توکن دسترسی جدید گوگل دریافت نشد.");
        }

        tokenResponse.RefreshToken ??= refreshToken;
        return MapTokenResult(tokenResponse);
    }

    public async Task<CloudProviderAccountInfo> GetAccountInfoAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "دریافت اطلاعات حساب گوگل ناموفق بود.", cancellationToken);

        var userInfo = await DeserializeAsync<GoogleUserInfoApiResponse>(response, cancellationToken)
            ?? throw new InvalidOperationException("اطلاعات حساب گوگل نامعتبر است.");

        if (string.IsNullOrWhiteSpace(userInfo.Sub))
        {
            throw new InvalidOperationException("شناسه حساب گوگل دریافت نشد.");
        }

        return new CloudProviderAccountInfo
        {
            ProviderAccountId = userInfo.Sub,
            Email = userInfo.Email ?? string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(userInfo.Name)
                ? userInfo.Email ?? userInfo.Sub
                : userInfo.Name
        };
    }

    public async Task<CloudStorageUsage> GetStorageUsageAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString(_options.DriveAboutEndpoint, new Dictionary<string, string?>
        {
            ["fields"] = "storageQuota"
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "دریافت میزان فضای ذخیره‌سازی گوگل درایو ناموفق بود.", cancellationToken);

        var about = await DeserializeAsync<GoogleDriveAboutApiResponse>(response, cancellationToken);
        var quota = about?.StorageQuota;

        long? total = ParseLong(quota?.Limit);
        long? used = ParseLong(quota?.Usage);
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

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret)
            || string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            throw new InvalidOperationException("پیکربندی گوگل درایو ناقص است.");
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            // Read body for diagnostics without logging secrets; discard content.
            _ = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(failureMessage);
        }
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static CloudOAuthTokenResult MapTokenResult(GoogleTokenApiResponse tokenResponse)
    {
        DateTime? expiresAt = tokenResponse.ExpiresIn is > 0
            ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn.Value)
            : null;

        return new CloudOAuthTokenResult
        {
            AccessToken = tokenResponse.AccessToken!,
            RefreshToken = tokenResponse.RefreshToken,
            AccessTokenExpiresAtUtc = expiresAt,
            TokenType = tokenResponse.TokenType,
            Scope = tokenResponse.Scope
        };
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, out var parsed) ? parsed : null;
    }

    private sealed class GoogleTokenApiResponse
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

    private sealed class GoogleUserInfoApiResponse
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class GoogleDriveAboutApiResponse
    {
        [JsonPropertyName("storageQuota")]
        public GoogleStorageQuotaApiResponse? StorageQuota { get; set; }
    }

    private sealed class GoogleStorageQuotaApiResponse
    {
        [JsonPropertyName("limit")]
        public string? Limit { get; set; }

        [JsonPropertyName("usage")]
        public string? Usage { get; set; }
    }
}
