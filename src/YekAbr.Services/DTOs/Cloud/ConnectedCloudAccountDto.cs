using YekAbr.Domain.Enums;

namespace YekAbr.Services.DTOs.Cloud;

public sealed class ConnectedCloudAccountDto
{
    public Guid Id { get; set; }
    public CloudProviderType Provider { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
}
