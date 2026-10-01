using FluentValidation;

namespace MusicLounge.Application.FnbMenus.Commands.DeleteFnbMenu;

public sealed class DeleteFnbMenuCommandValidator : AbstractValidator<DeleteFnbMenuCommand>
{
    public DeleteFnbMenuCommandValidator()
    {
        RuleFor(x => x.MenuId).NotEmpty().WithMessage("MenuId không hợp lệ.");
    }
}
