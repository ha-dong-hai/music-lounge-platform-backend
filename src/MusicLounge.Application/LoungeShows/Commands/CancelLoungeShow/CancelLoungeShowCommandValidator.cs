using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.CancelLoungeShow;

public sealed class CancelLoungeShowCommandValidator : AbstractValidator<CancelLoungeShowCommand>
{
    public CancelLoungeShowCommandValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty();
    }
}
