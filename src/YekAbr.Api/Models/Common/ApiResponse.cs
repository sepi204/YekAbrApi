namespace YekAbr.Api.Models.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> FromResult(bool success, string message, T? data = default, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = success,
            Message = message,
            Data = data,
            Errors = errors
        };
    }
}
