using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeAreaLayoutImage;

public sealed class SetLoungeAreaLayoutImageCommandValidator : AbstractValidator<SetLoungeAreaLayoutImageCommand>
{
    public SetLoungeAreaLayoutImageCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.ImageUrl).MaximumLength(500);
    }
}
