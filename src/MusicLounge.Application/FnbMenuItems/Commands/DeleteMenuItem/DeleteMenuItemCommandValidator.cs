using FluentValidation;

namespace MusicLounge.Application.FnbMenuItems.Commands.DeleteMenuItem;

public sealed class DeleteMenuItemCommandValidator : AbstractValidator<DeleteMenuItemCommand>
{
    public DeleteMenuItemCommandValidator()
    {
        RuleFor(x => x.MenuItemId).GreaterThan(0).WithMessage("MenuItemId không hợp lệ.");
    }
}
