using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.InitiateTicketTransfer;

// Buoc 1/2 cua chuyen nhuong ve: nguoi giu ve hien tai moi mot user khac nhan ve qua email.
// Ve bi "dong bang" (khong cho check-in/huy) cho toi khi accept/cancel de tranh vua chuyen vua dung.
internal sealed class InitiateTicketTransferCommandHandler
    : IRequestHandler<InitiateTicketTransferCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ITicketRepository _ticketRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public InitiateTicketTransferCommandHandler(
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

    public async Task<Unit> Handle(InitiateTicketTransferCommand request, CancellationToken ct)
    {
        var ticket = await _ticketRepo.GetByIdWithDetailsTrackedAsync(request.TicketId, ct)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        if (ticket.BuyerId != _currentUser.UserId)
            throw new ForbiddenException("Vé này không thuộc về bạn.");

        if (ticket.Status != TicketStatus.Confirmed)
            throw new DomainException("Chỉ có thể chuyển nhượng vé đã xác nhận.");

        if (ticket.PendingTransferToUserId is not null)
            throw new ConflictException("Vé này đang có một yêu cầu chuyển nhượng khác đang chờ xử lý.");

        if (ticket.Show.Status is LoungeShowStatus.Ended or LoungeShowStatus.Cancelled)
            throw new DomainException("Không thể chuyển nhượng vé của event đã kết thúc hoặc đã hủy.");

        if (ticket.PhysicalDetail?.CheckedInAt is not null)
            throw new DomainException("Vé đã check-in, không thể chuyển nhượng.");

        if (ticket.LivestreamDetail?.FirstAccessedAt is not null)
            throw new DomainException("Vé đã được dùng để xem livestream, không thể chuyển nhượng.");

        var recipients = await _uow.Repository<User, int>()
            .FindAsync(u => u.Email == request.RecipientEmail, ct);
        var recipient = recipients.FirstOrDefault()
            ?? throw new NotFoundException("Người nhận (theo email)", request.RecipientEmail);

        if (recipient.Id == _currentUser.UserId)
            throw new DomainException("Không thể chuyển nhượng vé cho chính mình.");

        ticket.PendingTransferToUserId = recipient.Id;
        ticket.PendingTransferInitiatedAt = DateTimeOffset.UtcNow;
        _ticketRepo.Update(ticket);

        await _notifications.NotifyAsync(
            recipient.Id,
            NotificationType.EventReminder,
            "Bạn nhận được lời mời chuyển nhượng vé",
            $"Ai đó muốn chuyển vé \"{ticket.Show.Name}\" cho bạn. Vào mục Vé của tôi để chấp nhận hoặc từ chối.",
            referenceType: "ticket",
            referenceId: ticket.Id.ToString(),
            ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
