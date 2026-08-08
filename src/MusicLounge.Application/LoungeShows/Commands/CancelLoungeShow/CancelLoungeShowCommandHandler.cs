using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.CancelLoungeShow;

// Huy ca show (nang hon ChangeLoungeShowFormat - show khong con dien ra nua) nen phai it nhat
// hao phong bang: hoan 100% CHO MOI ve Confirmed (khong chi rieng Physical nhu doi format,
// vi ca ve Livestream cung mat gia tri khi show bi huy hoan toan) + notify tung ticket holder.
internal sealed class CancelLoungeShowCommandHandler : IRequestHandler<CancelLoungeShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public CancelLoungeShowCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(CancelLoungeShowCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền hủy event này.");

        if (show.Status is LoungeShowStatus.Cancelled or LoungeShowStatus.Ended)
            throw new DomainException("Event đã kết thúc hoặc đã bị hủy trước đó.");

        show.Status = LoungeShowStatus.Cancelled;
        showRepo.Update(show);

        var ticketRepo = _uow.Repository<Ticket, Guid>();
        var confirmedTickets = await ticketRepo.FindAsync(
            t => t.ShowId == show.Id && t.Status == TicketStatus.Confirmed, ct);

        if (confirmedTickets.Count > 0)
        {
            var priceIds = confirmedTickets.Select(t => t.PriceId).Distinct().ToList();
            var prices = await _uow.Repository<TicketPrice, int>().FindAsync(p => priceIds.Contains(p.Id), ct);
            var priceById = prices.ToDictionary(p => p.Id, p => p.Price);

            var refundRepo = _uow.Repository<RefundRequest, int>();
            var now = DateTimeOffset.UtcNow;

            foreach (var ticket in confirmedTickets)
            {
                ticket.Status = TicketStatus.Cancelled;
                ticketRepo.Update(ticket);

                if (ticket.PaymentId is null) continue;

                refundRepo.Add(new RefundRequest
                {
                    PaymentId = ticket.PaymentId.Value,
                    RequestedBy = ticket.BuyerId,
                    Reason = "Event bị hủy — hoàn 100% tiền vé",
                    AmountRequested = priceById.GetValueOrDefault(ticket.PriceId),
                    RefundPercentage = 100m,
                    Status = RefundRequestStatus.Pending,
                    CreatedAt = now
                });

                if (ticket.BuyerId is int buyerId)
                    await _notifications.NotifyAsync(
                        buyerId,
                        NotificationType.EventReminder,
                        "Event đã bị hủy",
                        $"\"{show.Name}\" đã bị hủy. Vé của bạn đã được hủy và tự động tạo yêu cầu " +
                        "hoàn 100% tiền vé.",
                        referenceType: "show",
                        referenceId: show.Id.ToString(),
                        ct: ct);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
