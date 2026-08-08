using FluentValidation;

namespace MusicLounge.Api.Validators;

/// <summary>
/// Was hand-rolled null/size checks directly in UploadsController — moved here for consistency
/// with how every other command/query in this API validates its input (FluentValidation), and
/// because IFormFile is an ASP.NET Core type the Application layer deliberately never references
/// (IFileStorageService takes a raw Stream), so this can't live alongside the MediatR validators.
/// </summary>
internal sealed class UploadImageValidator : AbstractValidator<IFormFile>
{
    public const long MaxSizeBytes = 5 * 1024 * 1024; // 5MB

    public UploadImageValidator()
    {
        RuleFor(f => f).NotNull().WithMessage("Vui lòng chọn file ảnh.");

        RuleFor(f => f.Length)
            .GreaterThan(0).WithMessage("Vui lòng chọn file ảnh.")
            .LessThanOrEqualTo(MaxSizeBytes).WithMessage("File ảnh không được vượt quá 5MB.")
            .When(f => f is not null);
    }
}
