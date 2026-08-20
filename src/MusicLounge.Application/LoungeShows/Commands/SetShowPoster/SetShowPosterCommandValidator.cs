using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.SetShowPoster;

public sealed class SetShowPosterCommandValidator : AbstractValidator<SetShowPosterCommand>
{
    public SetShowPosterCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500);
    }
}
