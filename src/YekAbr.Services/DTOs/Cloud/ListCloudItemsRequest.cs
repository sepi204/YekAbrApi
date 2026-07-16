namespace YekAbr.Services.DTOs.Cloud;

public sealed class ListCloudItemsRequest
{
    public string? ParentId { get; set; }
    public int PageSize { get; set; } = 50;
    public string? PageToken { get; set; }
    public string? Search { get; set; }
    public bool IncludeFolders { get; set; } = true;
    public bool IncludeFiles { get; set; } = true;
}
