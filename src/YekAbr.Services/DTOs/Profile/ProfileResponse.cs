namespace YekAbr.Services.DTOs.Profile;

public sealed class ProfileResponse
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified URL suitable for frontend display, or null when no image is set.
    /// </summary>
    public string? ProfileImageUrl { get; set; }
}
