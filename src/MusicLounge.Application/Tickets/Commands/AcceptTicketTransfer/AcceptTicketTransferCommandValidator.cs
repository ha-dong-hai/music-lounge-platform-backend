using FluentValidation;

namespace MusicLounge.Application.Tickets.Commands.AcceptTicketTransfer;

public sealed class AcceptTicketTransferCommandValidator
    : AbstractValidator<AcceptTicketTransferCommand>
{
    // Guid.Empty đi lọt tới handler sẽ thành một lượt tra cứu không bao giờ khớp, rồi trả 404 —
    // đúng mã lỗi của "vé không tồn tại", che mất chuyện client gửi thiếu tham số.
    public AcceptTicketTransferCommandValidator()
        => RuleFor(x => x.TicketId).NotEmpty();
}
