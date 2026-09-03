using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.EndLoungeShow;

public sealed class EndLoungeShowCommandValidator : AbstractValidator<EndLoungeShowCommand>
{
    public EndLoungeShowCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
    }
}
