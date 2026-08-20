using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbOrders.Commands.CreateFnbOrder;

public sealed class CreateFnbOrderCommandValidator : AbstractValidator<CreateFnbOrderCommand>
{
    // "Gateway" used to be accepted here too, but nothing in CreateFnbOrderCommandHandler or
    // UpdateFnbOrderStatusCommandHandler ever calls VNPay for an FnbOrder — it was a purely
    // decorative option that behaved identically to Cash while implying an online-payment flow
    // that doesn't exist. Reject it explicitly instead of silently no-op'ing.
    private static readonly string[] ValidMethods = ["Cash"];

    public CreateFnbOrderCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.LoungeId)
            .Cascade(CascadeMode.Stop)
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
            .WithMessage("PaymentMethod phải là 'Cash' — F&B chưa hỗ trợ thanh toán qua cổng VNPay.");

        RuleFor(x => x.Items).NotEmpty().WithMessage("Đơn hàng phải có ít nhất 1 món.");

        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.MenuItemId).GreaterThan(0);
            i.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}
