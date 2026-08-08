using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.UpdateLoungeShow;

public sealed class UpdateLoungeShowCommandValidator : AbstractValidator<UpdateLoungeShowCommand>
{
    public UpdateLoungeShowCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ScheduledStart).GreaterThan(DateTimeOffset.UtcNow);
        RuleFor(x => x.ScheduledEnd)
            .GreaterThan(x => x.ScheduledStart)
            .When(x => x.ScheduledEnd.HasValue);
    }
}
