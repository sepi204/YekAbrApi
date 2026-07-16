using FluentValidation;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Validators.Cloud;

public sealed class ConnectMegaAccountRequestValidator : AbstractValidator<ConnectMegaAccountRequest>
{
    public ConnectMegaAccountRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("ایمیل حساب مگا الزامی است.")
            .EmailAddress().WithMessage("ایمیل حساب مگا معتبر نیست.")
            .MaximumLength(320).WithMessage("ایمیل حساب مگا بیش از حد طولانی است.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("رمز عبور حساب مگا الزامی است.")
            .MaximumLength(256).WithMessage("رمز عبور حساب مگا بیش از حد طولانی است.");

        RuleFor(x => x.MfaKey)
            .MaximumLength(128).WithMessage("کلید احراز هویت دو مرحله‌ای مگا بیش از حد طولانی است.")
            .When(x => !string.IsNullOrWhiteSpace(x.MfaKey));
    }
}
