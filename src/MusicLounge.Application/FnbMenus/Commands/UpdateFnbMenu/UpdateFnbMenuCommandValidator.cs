using FluentValidation;

namespace MusicLounge.Application.FnbMenus.Commands.UpdateFnbMenu;

public sealed class UpdateFnbMenuCommandValidator : AbstractValidator<UpdateFnbMenuCommand>
{
    public UpdateFnbMenuCommandValidator()
    {
        RuleFor(x => x.MenuId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
