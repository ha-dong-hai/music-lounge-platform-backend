using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeAreaLayoutImage;

public sealed class SetLoungeAreaLayoutImageCommandValidator : AbstractValidator<SetLoungeAreaLayoutImageCommand>
{
    public SetLoungeAreaLayoutImageCommandValidator()
    {
        RuleFor(x => x.LoungeId).NotEmpty();
        RuleFor(x => x.ImageUrl).MaximumLength(500);
    }
}
