using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeImage;

public sealed class SetLoungeImageCommandValidator : AbstractValidator<SetLoungeImageCommand>
{
    public SetLoungeImageCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500);
    }
}
