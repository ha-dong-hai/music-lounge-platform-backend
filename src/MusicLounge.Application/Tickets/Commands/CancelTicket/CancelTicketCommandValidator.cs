using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.CancelTicket;

public sealed class CancelTicketCommandValidator : AbstractValidator<CancelTicketCommand>
{
    public CancelTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
    }
}
