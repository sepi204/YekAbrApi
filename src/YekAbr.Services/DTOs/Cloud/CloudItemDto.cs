using YekAbr.Domain.Enums;
using YekAbr.Domain.Models;

namespace YekAbr.Services.DTOs.Cloud;

public sealed class CloudItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FullPath { get; set; }
    public CloudItemType ItemType { get; set; }
    public long? Size { get; set; }
    public string? MimeType { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ParentId { get; set; }
    public bool CanDownload { get; set; }
    public bool CanDelete { get; set; }
    public bool CanMove { get; set; }

    public static CloudItemDto FromDomain(CloudItem item)
    {
        return new CloudItemDto
        {
            Id = item.Id,
            Name = item.Name,
            FullPath = item.FullPath,
            ItemType = item.ItemType,
            Size = item.Size,
            MimeType = item.MimeType,
            ModifiedAtUtc = item.ModifiedAtUtc,
            ParentId = item.ParentId,
            CanDownload = item.CanDownload,
            CanDelete = item.CanDelete,
            CanMove = item.CanMove
        };
    }
}
