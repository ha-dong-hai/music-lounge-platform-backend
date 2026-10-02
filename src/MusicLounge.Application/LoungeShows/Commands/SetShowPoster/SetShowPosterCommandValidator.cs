using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.SetShowPoster;

public sealed class SetShowPosterCommandValidator : AbstractValidator<SetShowPosterCommand>
{
    public SetShowPosterCommandValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty();
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500);
    }
}
