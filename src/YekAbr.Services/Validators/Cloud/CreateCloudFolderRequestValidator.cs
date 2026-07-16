using FluentValidation;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Validators.Cloud;

public sealed class CreateCloudFolderRequestValidator : AbstractValidator<CreateCloudFolderRequest>
{
    public CreateCloudFolderRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام پوشه الزامی است.")
            .MaximumLength(255).WithMessage("نام پوشه نباید بیشتر از ۲۵۵ کاراکتر باشد.");
    }
}
