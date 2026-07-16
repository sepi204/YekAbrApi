namespace YekAbr.Domain.Models;

/// <summary>
/// Provider-neutral storage quota summary for a connected cloud account.
/// </summary>
public sealed class CloudStorageUsage
{
    public long? TotalBytes { get; set; }
    public long? UsedBytes { get; set; }
    public long? FreeBytes { get; set; }
}
