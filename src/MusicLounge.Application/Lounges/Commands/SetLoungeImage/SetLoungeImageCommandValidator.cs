using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeImage;

public sealed class SetLoungeImageCommandValidator : AbstractValidator<SetLoungeImageCommand>
{
    public SetLoungeImageCommandValidator()
    {
        RuleFor(x => x.LoungeId).NotEmpty();
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500);
    }
}
