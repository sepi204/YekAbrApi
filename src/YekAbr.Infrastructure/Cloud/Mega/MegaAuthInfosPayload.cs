namespace YekAbr.Infrastructure.Cloud.Mega;

/// <summary>
/// Serializable durable MEGA credentials (AuthInfos) stored encrypted as AccessToken.
/// </summary>
internal sealed class MegaAuthInfosPayload
{
    public string Email { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public byte[] PasswordAesKey { get; set; } = [];
    public string? MfaKey { get; set; }
}
