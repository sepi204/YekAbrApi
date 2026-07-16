namespace YekAbr.Services.DTOs.Cloud;

public sealed class UploadCloudFileRequest
{
    public Stream Content { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? ParentFolderId { get; set; }
    public long? ContentLength { get; set; }
}
