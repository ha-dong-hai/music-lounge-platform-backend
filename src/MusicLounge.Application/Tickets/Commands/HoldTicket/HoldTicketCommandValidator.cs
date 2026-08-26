using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Tickets.Commands.HoldTicket;

internal sealed class HoldTicketCommandValidator : AbstractValidator<HoldTicketCommand>
{
    public HoldTicketCommandValidator(IUnitOfWork uow, ISystemConfigService config)
    {
        RuleFor(x => x.PriceId)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("PriceId không hợp lệ.")
            .MustAsync(async (priceId, ct) => await uow.Repository<TicketPrice, int>().AnyAsync(p => p.Id == priceId, ct))
            .WithMessage("PriceId không tồn tại.");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1)
            .MustAsync(async (_, qty, context, ct) =>
            {
                var max = await config.GetIntAsync(ConfigKeys.TicketHoldMaxQuantity, 10, ct);
                context.MessageFormatter.AppendArgument("MaxQuantity", max);
                return qty <= max;
            })
            .WithMessage("Số lượng vé phải từ 1 đến {MaxQuantity}.");
    }
}
