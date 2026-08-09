using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.StitchVenueTourScene;

public sealed class StitchVenueTourSceneCommandValidator : AbstractValidator<StitchVenueTourSceneCommand>
{
    public StitchVenueTourSceneCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.Name).MaximumLength(100);

        RuleFor(x => x.SourceImageUrls)
            .NotEmpty().WithMessage("Cần ít nhất 2 ảnh để ghép panorama.")
            .Must(urls => urls.Count >= 2).WithMessage("Cần ít nhất 2 ảnh để ghép panorama.")
            .Must(urls => urls.Count <= 20).WithMessage("Tối đa 20 ảnh cho mỗi lần ghép.");

        RuleForEach(x => x.SourceImageUrls)
            .NotEmpty()
            .MaximumLength(500);
    }
}
