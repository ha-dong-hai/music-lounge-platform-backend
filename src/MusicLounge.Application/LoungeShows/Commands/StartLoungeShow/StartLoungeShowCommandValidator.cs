using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.StartLoungeShow;

public sealed class StartLoungeShowCommandValidator : AbstractValidator<StartLoungeShowCommand>
{
    public StartLoungeShowCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
    }
}
