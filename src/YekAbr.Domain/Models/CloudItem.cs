using YekAbr.Domain.Enums;

namespace YekAbr.Domain.Models;

/// <summary>
/// Provider-neutral representation of a file or folder in a connected cloud account.
/// </summary>
public sealed class CloudItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public CloudItemType ItemType { get; set; }
    public long? Size { get; set; }
    public string? MimeType { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ParentId { get; set; }
    public bool CanDownload { get; set; }
    public bool CanDelete { get; set; }
    public bool CanMove { get; set; }
}
