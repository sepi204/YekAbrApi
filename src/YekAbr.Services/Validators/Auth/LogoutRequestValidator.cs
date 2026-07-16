using FluentValidation;
using YekAbr.Services.DTOs.Auth;

namespace YekAbr.Services.Validators.Auth;

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
