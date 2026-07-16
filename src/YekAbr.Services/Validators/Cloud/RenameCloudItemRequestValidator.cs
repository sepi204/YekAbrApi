using FluentValidation;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Validators.Cloud;

public sealed class RenameCloudItemRequestValidator : AbstractValidator<RenameCloudItemRequest>
{
    public RenameCloudItemRequestValidator()
    {
        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("نام جدید الزامی است.")
            .MaximumLength(255).WithMessage("نام جدید نباید بیشتر از ۲۵۵ کاراکتر باشد.");
    }
}
