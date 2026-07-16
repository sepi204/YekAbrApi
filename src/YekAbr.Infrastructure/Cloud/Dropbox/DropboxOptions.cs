namespace YekAbr.Infrastructure.Cloud.Dropbox;

public sealed class DropboxOptions
{
    public const string SectionName = "Dropbox";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = "https://www.dropbox.com/oauth2/authorize";
    public string TokenEndpoint { get; set; } = "https://api.dropboxapi.com/oauth2/token";
    public string ApiEndpoint { get; set; } = "https://api.dropboxapi.com/2";
    public string ContentEndpoint { get; set; } = "https://content.dropboxapi.com/2";

    /// <summary>
    /// Dropbox scoped apps typically use no explicit scope string; token_access_type=offline requests refresh tokens.
    /// </summary>
    public string? Scopes { get; set; }

    public string FrontendSuccessRedirectUrl { get; set; } = string.Empty;
    public string FrontendFailureRedirectUrl { get; set; } = string.Empty;
    public int OAuthStateLifetimeMinutes { get; set; } = 10;
}
