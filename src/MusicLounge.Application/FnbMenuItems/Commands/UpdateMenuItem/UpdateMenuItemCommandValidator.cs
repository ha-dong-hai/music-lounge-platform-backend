using FluentValidation;

namespace MusicLounge.Application.FnbMenuItems.Commands.UpdateMenuItem;

public sealed class UpdateMenuItemCommandValidator : AbstractValidator<UpdateMenuItemCommand>
{
    public UpdateMenuItemCommandValidator()
    {
        RuleFor(x => x.MenuItemId).GreaterThan(0);
        // 50, not 100 — matches FnbMenuItemConfiguration's actual HasMaxLength(50), same fix as
        // CreateMenuItemCommandValidator.
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.ImageUrl).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}
