using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.InitiateTicketTransfer;

public sealed class InitiateTicketTransferCommandValidator : AbstractValidator<InitiateTicketTransferCommand>
{
    public InitiateTicketTransferCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.RecipientEmail).NotEmpty().EmailAddress();
    }
}
