using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.CancelTicketTransfer;

public sealed class CancelTicketTransferCommandValidator : AbstractValidator<CancelTicketTransferCommand>
{
    public CancelTicketTransferCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
    }
}
