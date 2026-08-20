using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.AddLoungeGalleryImage;

public sealed class AddLoungeGalleryImageCommandValidator : AbstractValidator<AddLoungeGalleryImageCommand>
{
    public AddLoungeGalleryImageCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Caption).MaximumLength(255);
    }
}
