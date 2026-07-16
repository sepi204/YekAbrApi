namespace YekAbr.Services.DTOs.Cloud;

public sealed class CloudOAuthTokenResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string? TokenType { get; set; }
    public string? Scope { get; set; }
}
