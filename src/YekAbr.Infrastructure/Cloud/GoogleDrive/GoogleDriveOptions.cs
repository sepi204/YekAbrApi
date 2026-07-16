namespace YekAbr.Infrastructure.Cloud.GoogleDrive;

public sealed class GoogleDriveOptions
{
    public const string SectionName = "GoogleDrive";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenEndpoint { get; set; } = "https://oauth2.googleapis.com/token";
    public string UserInfoEndpoint { get; set; } = "https://www.googleapis.com/oauth2/v3/userinfo";
    public string DriveAboutEndpoint { get; set; } = "https://www.googleapis.com/drive/v3/about";

    /// <summary>
    /// Includes identity scopes plus full Drive access for future file-management phases.
    /// </summary>
    public string[] Scopes { get; set; } =
    [
        "openid",
        "email",
        "profile",
        "https://www.googleapis.com/auth/drive"
    ];

    public string FrontendSuccessRedirectUrl { get; set; } = string.Empty;
    public string FrontendFailureRedirectUrl { get; set; } = string.Empty;
    public int OAuthStateLifetimeMinutes { get; set; } = 10;
}
