using FluentValidation;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Tickets.Commands.SellWalkInTicket;

public sealed class SellWalkInTicketCommandValidator : AbstractValidator<SellWalkInTicketCommand>
{
    public SellWalkInTicketCommandValidator(ISystemConfigService config)
    {
        RuleFor(x => x.PriceId).GreaterThan(0);

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .MustAsync(async (_, qty, context, ct) =>
            {
                var max = await config.GetIntAsync(ConfigKeys.WalkInTicketMaxQuantity, 20, ct);
                context.MessageFormatter.AppendArgument("MaxQuantity", max);
                return qty <= max;
            })
            .WithMessage("Số lượng vé bán tại quầy phải từ 1 đến {MaxQuantity}.");
    }
}
