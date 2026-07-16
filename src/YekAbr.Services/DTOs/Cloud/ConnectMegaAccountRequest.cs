namespace YekAbr.Services.DTOs.Cloud;

public sealed class ConnectMegaAccountRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional multi-factor authentication key when MEGA MFA is enabled.
    /// </summary>
    public string? MfaKey { get; set; }
}
