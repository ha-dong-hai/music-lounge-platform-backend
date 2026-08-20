using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.AddVenueTourScene;

public sealed class AddVenueTourSceneCommandValidator : AbstractValidator<AddVenueTourSceneCommand>
{
    public AddVenueTourSceneCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Name).MaximumLength(100);
    }
}
