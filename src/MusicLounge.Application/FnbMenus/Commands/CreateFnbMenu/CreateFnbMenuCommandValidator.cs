using FluentValidation;

namespace MusicLounge.Application.FnbMenus.Commands.CreateFnbMenu;

public sealed class CreateFnbMenuCommandValidator : AbstractValidator<CreateFnbMenuCommand>
{
    public CreateFnbMenuCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
