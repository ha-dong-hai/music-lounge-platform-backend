using FluentValidation;

namespace MusicLounge.Api.Validators;

/// <summary>See UploadImageValidator — same rationale, separate rules (30MB cap for .glb models).</summary>
internal sealed class UploadModel3DValidator : AbstractValidator<IFormFile>
{
    public const long MaxSizeBytes = 30 * 1024 * 1024; // 30MB — file .glb thuc te co the kha lon

    public UploadModel3DValidator()
    {
        RuleFor(f => f).NotNull().WithMessage("Vui lòng chọn file mô hình 3D.");

        RuleFor(f => f.Length)
            .GreaterThan(0).WithMessage("Vui lòng chọn file mô hình 3D.")
            .LessThanOrEqualTo(MaxSizeBytes).WithMessage("File mô hình 3D không được vượt quá 30MB.")
            .When(f => f is not null);
    }
}
