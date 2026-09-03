using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.RescheduleLoungeShow;

public sealed class RescheduleLoungeShowCommandValidator : AbstractValidator<RescheduleLoungeShowCommand>
{
    public RescheduleLoungeShowCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
        RuleFor(x => x.NewScheduledStart).GreaterThan(DateTimeOffset.UtcNow);
    }
}
