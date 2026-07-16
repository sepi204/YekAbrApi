namespace YekAbr.Services.DTOs.Cloud;

public sealed class CreateCloudFolderRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ParentFolderId { get; set; }
}
