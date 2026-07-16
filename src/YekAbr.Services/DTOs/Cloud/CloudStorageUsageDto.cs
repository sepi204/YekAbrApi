namespace YekAbr.Services.DTOs.Cloud;

public sealed class CloudStorageUsageDto
{
    public long? TotalBytes { get; set; }
    public long? UsedBytes { get; set; }
    public long? FreeBytes { get; set; }
}
