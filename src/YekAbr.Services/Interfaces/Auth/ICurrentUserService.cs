namespace YekAbr.Services.Interfaces.Auth;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Username { get; }
}
