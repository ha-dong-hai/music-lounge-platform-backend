using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.FnbMenuItems.Commands.CreateMenuItem;

public sealed class CreateMenuItemCommandValidator : AbstractValidator<CreateMenuItemCommand>
{
    public CreateMenuItemCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.MenuId)
            .GreaterThan(0)
            .MustAsync(async (menuId, ct) => await uow.Repository<FnbMenu, int>().AnyAsync(m => m.Id == menuId, ct))
            .WithMessage("MenuId không tồn tại.");
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}
