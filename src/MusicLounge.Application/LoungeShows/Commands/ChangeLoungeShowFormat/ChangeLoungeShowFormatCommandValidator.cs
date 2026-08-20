using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.ChangeLoungeShowFormat;

public sealed class ChangeLoungeShowFormatCommandValidator : AbstractValidator<ChangeLoungeShowFormatCommand>
{
    public ChangeLoungeShowFormatCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
        RuleFor(x => x.NewFormat).IsInEnum();
    }
}
