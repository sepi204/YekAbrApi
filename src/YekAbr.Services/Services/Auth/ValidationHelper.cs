using FluentValidation;
using YekAbr.Services.Common.Responses;

namespace YekAbr.Services.Services.Auth;

public static class ValidationHelper
{
    public static async Task<IReadOnlyDictionary<string, string[]>?> ValidateAsync<T>(IValidator<T> validator, T model, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(model, cancellationToken);
        if (validationResult.IsValid)
        {
            return null;
        }

        return validationResult.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                x => x.Key,
                x => x.Select(e => e.ErrorMessage).Distinct().ToArray() as string[]);
    }
}
