using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.CancelTicketTransfer;

// Nguoi gui huy truoc khi recipient tra loi, HOAC nguoi nhan tu choi - ca hai deu dua ve cung
// mot ket qua: xoa pending fields, ve giu nguyen o chu cu.
internal sealed class CancelTicketTransferCommandHandler : IRequestHandler<CancelTicketTransferCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ITicketRepository _ticketRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public CancelTicketTransferCommandHandler(
        IUnitOfWork uow,
        ITicketRepository ticketRepo,
        ICurrentUserService currentUser,
        INotificationService notifications)
    {
        _uow = uow;
        _ticketRepo = ticketRepo;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(CancelTicketTransferCommand request, CancellationToken ct)
    {
        var ticket = await _ticketRepo.GetByIdWithDetailsTrackedAsync(request.TicketId, ct)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        if (ticket.PendingTransferToUserId is null)
            throw new DomainException("Vé này không có yêu cầu chuyển nhượng nào đang chờ xử lý.");

        var isSender = ticket.BuyerId == _currentUser.UserId;
        var isRecipient = ticket.PendingTransferToUserId == _currentUser.UserId;

        if (!isSender && !isRecipient)
            throw new ForbiddenException("Bạn không liên quan đến yêu cầu chuyển nhượng vé này.");

        var recipientId = ticket.PendingTransferToUserId.Value;

        ticket.PendingTransferToUserId = null;
        ticket.PendingTransferInitiatedAt = null;
        _ticketRepo.Update(ticket);

        // Chi bao cho phia con lai - nguoi tu huy/tu choi thi khong can bao lai chinh minh.
        if (isSender)
            await _notifications.NotifyAsync(
                recipientId, NotificationType.EventReminder,
                "Yêu cầu chuyển nhượng vé đã bị hủy",
                $"Yêu cầu chuyển vé \"{ticket.Show.Name}\" cho bạn đã bị người gửi hủy.",
                referenceType: "ticket", referenceId: ticket.Id.ToString(), ct: ct);
        else if (ticket.BuyerId is int senderId)
            await _notifications.NotifyAsync(
                senderId, NotificationType.EventReminder,
                "Yêu cầu chuyển nhượng vé bị từ chối",
                $"Người nhận đã từ chối vé \"{ticket.Show.Name}\" bạn muốn chuyển.",
                referenceType: "ticket", referenceId: ticket.Id.ToString(), ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
