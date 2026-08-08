using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;

public sealed class CreateFnbOrderCommandValidator : AbstractValidator<CreateFnbOrderCommand>
{
    private static readonly string[] ValidMethods = ["Gateway", "Cash"];

    public CreateFnbOrderCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.LoungeId)
            .GreaterThan(0)
            .MustAsync(async (loungeId, ct) =>
                await uow.Repository<MusicLoungeEntity, int>().AnyAsync(l => l.Id == loungeId, ct))
            .WithMessage("LoungeId không tồn tại.");

        RuleFor(x => x.ZoneId)
            .MustAsync(async (zoneId, ct) =>
                await uow.Repository<SeatingZone, int>().AnyAsync(z => z.Id == zoneId!.Value, ct))
            .When(x => x.ZoneId.HasValue)
            .WithMessage("ZoneId không tồn tại.");

        RuleFor(x => x.ShowId)
            .MustAsync(async (showId, ct) =>
                await uow.Repository<LoungeShow, int>().AnyAsync(s => s.Id == showId!.Value, ct))
            .When(x => x.ShowId.HasValue)
            .WithMessage("ShowId không tồn tại.");

        RuleFor(x => x.PaymentMethod)
            .Must(m => ValidMethods.Contains(m, StringComparer.OrdinalIgnoreCase))
            .WithMessage("PaymentMethod phải là 'Gateway' hoặc 'Cash'.");

        RuleFor(x => x.Items).NotEmpty().WithMessage("Đơn hàng phải có ít nhất 1 món.");

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.MenuItemId).GreaterThan(0);
            i.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}
