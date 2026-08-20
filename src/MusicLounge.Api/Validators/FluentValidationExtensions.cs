using FluentValidation;
using AppValidationException = MusicLounge.Application.Common.Exceptions.ValidationException;

namespace MusicLounge.Api.Validators;

internal static class FluentValidationExtensions
{
    /// <summary>
    /// Runs a standalone (non-MediatR) FluentValidation validator and throws the same
    /// AppValidationException shape ValidationBehavior throws for every command/query, so a
    /// caller invoked outside the MediatR pipeline (like UploadsController's IFormFile params)
    /// still produces the identical {success,message,errors} response GlobalExceptionHandler
    /// renders for every other 400 in this API.
    /// </summary>
    public static async Task ValidateAndThrowAppExceptionAsync<T>(
        this IValidator<T> validator, T instance, CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (result.IsValid) return;

        var errors = result.Errors
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());

        throw new AppValidationException(errors);
    }
}
