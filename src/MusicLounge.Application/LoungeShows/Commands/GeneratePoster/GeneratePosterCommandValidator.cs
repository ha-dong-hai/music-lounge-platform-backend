using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.GeneratePoster;

public sealed class GeneratePosterCommandValidator : AbstractValidator<GeneratePosterCommand>
{
    public GeneratePosterCommandValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty();
        RuleFor(x => x.StyleHint).MaximumLength(500);
    }
}
