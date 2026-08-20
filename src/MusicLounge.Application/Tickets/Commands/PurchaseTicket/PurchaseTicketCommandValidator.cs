using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.PurchaseTicket;

internal sealed class PurchaseTicketCommandValidator : AbstractValidator<PurchaseTicketCommand>
{
    public PurchaseTicketCommandValidator()
    {
        RuleFor(x => x.HoldId).GreaterThan(0).WithMessage("HoldId không hợp lệ.");
        RuleFor(x => x.ClientIpAddress).NotEmpty().WithMessage("Địa chỉ IP không được rỗng.");
    }
}
