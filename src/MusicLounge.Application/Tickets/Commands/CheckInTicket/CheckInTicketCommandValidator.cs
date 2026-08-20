using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.CheckInTicket;

internal sealed class CheckInTicketCommandValidator : AbstractValidator<CheckInTicketCommand>
{
    public CheckInTicketCommandValidator()
    {
        RuleFor(x => x.QrCode).NotEmpty().WithMessage("Mã QR không được rỗng.");
    }
}
