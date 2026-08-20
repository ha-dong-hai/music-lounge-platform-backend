using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.SetShowCoverImage;

public sealed class SetShowCoverImageCommandValidator : AbstractValidator<SetShowCoverImageCommand>
{
    public SetShowCoverImageCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500);
    }
}
