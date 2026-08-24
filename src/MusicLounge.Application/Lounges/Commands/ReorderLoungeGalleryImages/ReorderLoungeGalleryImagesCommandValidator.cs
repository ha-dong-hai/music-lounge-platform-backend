using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.ReorderLoungeGalleryImages;

public sealed class ReorderLoungeGalleryImagesCommandValidator
    : AbstractValidator<ReorderLoungeGalleryImagesCommand>
{
    public ReorderLoungeGalleryImagesCommandValidator()
    {
        RuleFor(x => x.OrderedImageIds).NotEmpty().WithMessage("Danh sách thứ tự ảnh không được để trống.");
        RuleFor(x => x.OrderedImageIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Danh sách thứ tự ảnh có Id trùng lặp.")
            .When(x => x.OrderedImageIds.Count > 0);
    }
}
