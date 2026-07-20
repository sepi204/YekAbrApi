using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using YekAbr.Infrastructure.Options;
using YekAbr.Services.Interfaces.Profile;

namespace YekAbr.Infrastructure.Services.Profile;

public sealed class PublicUrlBuilder : IPublicUrlBuilder
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ProfileImageOptions _options;

    public PublicUrlBuilder(
        IHttpContextAccessor httpContextAccessor,
        IOptions<ProfileImageOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public string? ToAbsoluteUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        if (Uri.TryCreate(relativePath, UriKind.Absolute, out var absoluteUri)
            && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return absoluteUri.ToString();
        }

        var path = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is not null)
        {
            return $"{request.Scheme}://{request.Host.Value}{request.PathBase}{path}";
        }

        // Fallback when no HTTP context is available (e.g. background scope).
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return _options.PublicBaseUrl.TrimEnd('/') + path;
        }

        return path;
    }
}
