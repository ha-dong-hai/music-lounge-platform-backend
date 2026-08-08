using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.HoldTicket;

internal sealed class HoldTicketCommandValidator : AbstractValidator<HoldTicketCommand>
{
    public HoldTicketCommandValidator()
    {
        RuleFor(x => x.PriceId).GreaterThan(0).WithMessage("PriceId không hợp lệ.");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 10).WithMessage("Số lượng vé phải từ 1 đến 10.");
    }
}
