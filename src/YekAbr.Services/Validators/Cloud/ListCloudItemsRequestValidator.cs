using FluentValidation;
using YekAbr.Services.DTOs.Cloud;

namespace YekAbr.Services.Validators.Cloud;

public sealed class ListCloudItemsRequestValidator : AbstractValidator<ListCloudItemsRequest>
{
    public ListCloudItemsRequestValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200).WithMessage("اندازه صفحه باید بین ۱ تا ۲۰۰ باشد.");

        RuleFor(x => x)
            .Must(x => x.IncludeFiles || x.IncludeFolders)
            .WithMessage("حداقل یکی از گزینه‌های فایل یا پوشه باید فعال باشد.");
    }
}
