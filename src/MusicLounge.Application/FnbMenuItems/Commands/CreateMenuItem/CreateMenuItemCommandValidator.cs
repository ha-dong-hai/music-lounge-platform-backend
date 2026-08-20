using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.FnbMenuItems.Commands.CreateMenuItem;

public sealed class CreateMenuItemCommandValidator : AbstractValidator<CreateMenuItemCommand>
{
    public CreateMenuItemCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.MenuId)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0)
            .MustAsync(async (menuId, ct) => await uow.Repository<FnbMenu, int>().AnyAsync(m => m.Id == menuId, ct))
            .WithMessage("MenuId không tồn tại.");
        // 50, not 100 — matches FnbMenuItemConfiguration's actual HasMaxLength(50); a validator
        // more permissive than the DB column just delays the same error to a DbUpdateException
        // (generic 409) at SaveChangesAsync instead of a clean 400 naming the field.
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.ImageUrl).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}
