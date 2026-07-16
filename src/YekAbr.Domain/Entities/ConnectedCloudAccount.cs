using YekAbr.Domain.Enums;

namespace YekAbr.Domain.Entities;

public sealed class ConnectedCloudAccount
{
    public Guid Id { get; set; }

    /// <summary>
    /// Matches ASP.NET Identity AppUser.Id (string).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    public CloudProviderType Provider { get; set; }
    public string AccountEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ProviderAccountId { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted access token. Use ICloudTokenEncryptionService before persistence and after retrieval.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string? RootFolderId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
}
