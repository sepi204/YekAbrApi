namespace YekAbr.Services.DTOs.Profile;

public sealed class UpdateProfileRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
