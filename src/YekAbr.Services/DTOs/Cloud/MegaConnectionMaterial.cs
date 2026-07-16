namespace YekAbr.Services.DTOs.Cloud;

/// <summary>
/// Result of establishing a MEGA session for persistence.
/// AuthInfosJson must be encrypted before storage; never return it from APIs.
/// </summary>
public sealed class MegaConnectionMaterial
{
    public CloudProviderAccountInfo AccountInfo { get; set; } = new();
    public string AuthInfosJson { get; set; } = string.Empty;
    public string RootFolderId { get; set; } = string.Empty;
}
