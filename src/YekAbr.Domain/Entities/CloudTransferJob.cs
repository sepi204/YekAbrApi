using YekAbr.Domain.Enums;

namespace YekAbr.Domain.Entities;

public sealed class CloudTransferJob
{
    public Guid Id { get; set; }

    /// <summary>
    /// Matches ASP.NET Identity AppUser.Id (string).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    public Guid SourceConnectedCloudAccountId { get; set; }
    public Guid DestinationConnectedCloudAccountId { get; set; }
    public string SourceItemId { get; set; } = string.Empty;
    public string SourceItemName { get; set; } = string.Empty;
    public CloudItemType SourceItemType { get; set; }
    public string? DestinationParentFolderId { get; set; }
    public CloudTransferStatus Status { get; set; } = CloudTransferStatus.Pending;
    public int ProgressPercentage { get; set; }
    public long? TotalBytes { get; set; }
    public long? TransferredBytes { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public ConnectedCloudAccount? SourceConnectedCloudAccount { get; set; }
    public ConnectedCloudAccount? DestinationConnectedCloudAccount { get; set; }
}
