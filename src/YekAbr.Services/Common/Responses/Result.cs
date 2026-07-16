namespace YekAbr.Services.Common.Responses;

public sealed class Result<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static Result<T> Succeeded(T data, string message) => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static Result<T> Failed(string message, IReadOnlyDictionary<string, string[]>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors
    };
}
