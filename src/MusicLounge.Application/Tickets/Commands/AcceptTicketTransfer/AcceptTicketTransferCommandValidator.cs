using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.AcceptTicketTransfer;

public sealed class AcceptTicketTransferCommandValidator : AbstractValidator<AcceptTicketTransferCommand>
{
    public AcceptTicketTransferCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
    }
}
