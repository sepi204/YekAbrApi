using FluentValidation;
using YekAbr.Services.DTOs.Profile;

namespace YekAbr.Services.Validators.Profile;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("نام کاربری الزامی است.")
            .MinimumLength(3).WithMessage("نام کاربری باید حداقل ۳ کاراکتر باشد.")
            .MaximumLength(50).WithMessage("نام کاربری نباید بیشتر از ۵۰ کاراکتر باشد.")
            .Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("نام کاربری فقط می‌تواند شامل حروف، اعداد، نقطه، خط تیره و زیرخط باشد.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("ایمیل الزامی است.")
            .EmailAddress().WithMessage("فرمت ایمیل معتبر نیست.")
            .MaximumLength(256).WithMessage("ایمیل نباید بیشتر از ۲۵۶ کاراکتر باشد.");
    }
}
