using FluentValidation;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Validators.Cloud;

public sealed class MoveCloudItemRequestValidator : AbstractValidator<MoveCloudItemRequest>
{
    public MoveCloudItemRequestValidator()
    {
        RuleFor(x => x.DestinationParentFolderId)
            .NotEmpty().WithMessage("شناسه پوشه مقصد الزامی است.");
    }
}
