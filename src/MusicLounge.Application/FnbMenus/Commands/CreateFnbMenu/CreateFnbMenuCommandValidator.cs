using FluentValidation;

namespace MusicLounge.Application.FnbMenus.Commands.CreateFnbMenu;

public sealed class CreateFnbMenuCommandValidator : AbstractValidator<CreateFnbMenuCommand>
{
    public CreateFnbMenuCommandValidator()
    {
        RuleFor(x => x.LoungeId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
