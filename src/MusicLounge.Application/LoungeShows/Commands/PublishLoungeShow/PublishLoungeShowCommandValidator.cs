using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.PublishLoungeShow;

public sealed class PublishLoungeShowCommandValidator : AbstractValidator<PublishLoungeShowCommand>
{
    public PublishLoungeShowCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
    }
}
