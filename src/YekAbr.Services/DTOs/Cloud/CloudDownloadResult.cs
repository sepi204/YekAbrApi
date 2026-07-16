namespace YekAbr.Services.DTOs.Cloud;

/// <summary>
/// Streamed download payload. Caller must dispose the result to release provider resources.
/// </summary>
public sealed class CloudDownloadResult : IAsyncDisposable, IDisposable
{
    private readonly IDisposable? _lifetime;

    public CloudDownloadResult(
        Stream content,
        string fileName,
        string contentType,
        long? contentLength,
        IDisposable? lifetime = null)
    {
        Content = content;
        FileName = fileName;
        ContentType = contentType;
        ContentLength = contentLength;
        _lifetime = lifetime;
    }

    public Stream Content { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long? ContentLength { get; }

    public void Dispose()
    {
        Content.Dispose();
        _lifetime?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
        if (_lifetime is IAsyncDisposable asyncLifetime)
        {
            await asyncLifetime.DisposeAsync();
        }
        else
        {
            _lifetime?.Dispose();
        }
    }
}
