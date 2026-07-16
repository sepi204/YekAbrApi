using YekAbr.Domain.Models;

namespace YekAbr.Services.DTOs.Cloud;

public sealed class CloudItemListResult
{
    public IReadOnlyList<CloudItem> Items { get; set; } = Array.Empty<CloudItem>();
    public string? NextPageToken { get; set; }
}
