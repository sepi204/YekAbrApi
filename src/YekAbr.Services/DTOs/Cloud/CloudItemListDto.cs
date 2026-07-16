namespace YekAbr.Services.DTOs.Cloud;

public sealed class CloudItemListDto
{
    public IReadOnlyList<CloudItemDto> Items { get; set; } = Array.Empty<CloudItemDto>();
    public string? NextPageToken { get; set; }
}
