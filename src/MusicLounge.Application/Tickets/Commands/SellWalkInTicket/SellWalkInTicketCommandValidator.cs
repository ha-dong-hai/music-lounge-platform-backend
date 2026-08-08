using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.SellWalkInTicket;

public sealed class SellWalkInTicketCommandValidator : AbstractValidator<SellWalkInTicketCommand>
{
    public SellWalkInTicketCommandValidator()
    {
        RuleFor(x => x.PriceId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(20)
            .WithMessage("Số lượng vé bán tại quầy phải từ 1 đến 20.");
    }
}
