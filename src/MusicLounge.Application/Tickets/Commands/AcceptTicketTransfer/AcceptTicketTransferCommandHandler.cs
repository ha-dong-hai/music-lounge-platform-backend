using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.AcceptTicketTransfer;

// Buoc 2/2: nguoi duoc moi chap nhan - doi BuyerId, regenerate QR/AccessToken vi chu cu da tung
// thay ma cu (bao mat), clear pending fields.
internal sealed class AcceptTicketTransferCommandHandler : IRequestHandler<AcceptTicketTransferCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ITicketRepository _ticketRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public AcceptTicketTransferCommandHandler(
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

    public async Task<Unit> Handle(AcceptTicketTransferCommand request, CancellationToken ct)
    {
        var ticket = await _ticketRepo.GetByIdWithDetailsTrackedAsync(request.TicketId, ct)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        if (ticket.PendingTransferToUserId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không phải là người được mời nhận vé này.");

        if (ticket.Status != TicketStatus.Confirmed)
            throw new DomainException("Vé không còn hợp lệ để nhận chuyển nhượng.");

        if (ticket.Show.Status is LoungeShowStatus.Ended or LoungeShowStatus.Cancelled)
            throw new DomainException("Event đã kết thúc hoặc đã hủy, không thể nhận chuyển nhượng vé.");

        var previousBuyerId = ticket.BuyerId;

        ticket.BuyerId = _currentUser.UserId;
        ticket.QrCode = Guid.NewGuid().ToString("N");
        ticket.PendingTransferToUserId = null;
        ticket.PendingTransferInitiatedAt = null;

        if (ticket.LivestreamDetail is not null)
            ticket.LivestreamDetail.AccessToken = Guid.NewGuid().ToString("N");

        _ticketRepo.Update(ticket);

        if (previousBuyerId is int prevId)
            await _notifications.NotifyAsync(
                prevId,
                NotificationType.EventReminder,
                "Chuyển nhượng vé thành công",
                $"Vé \"{ticket.Show.Name}\" bạn chuyển đã được người nhận chấp nhận.",
                referenceType: "ticket",
                referenceId: ticket.Id.ToString(),
                ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
