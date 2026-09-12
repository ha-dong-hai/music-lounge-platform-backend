using MediatR;
using MusicLounge.Application.FnbOrders;
using MusicLounge.Application.Tickets;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.ChangeLoungeShowFormat;

// D13: doi format Offline -> Online sau khi da co physical ticket ban ra - bat buoc hoan 100%
// cho tung physical ticket da Confirmed (khong phu thuoc show.RefundPercentage).
internal sealed class ChangeLoungeShowFormatCommandHandler : IRequestHandler<ChangeLoungeShowFormatCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly IAsyncKeyedLock _lock;

    public ChangeLoungeShowFormatCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _lock = @lock;
    }

    public async Task<Unit> Handle(ChangeLoungeShowFormatCommand request, CancellationToken ct)
    {
        // Without this, a double-click (or Owner + Admin racing) both pass the Format==Offline
        // guard before either commits, and both generate a duplicate RefundRequest per physical
        // ticket — not a double-payout (ProcessRefundRequestCommandHandler's cumulative check
        // still caps total refunds at payment.GrossAmount), but a stuck duplicate Pending refund
        // row and a duplicate cancellation notification per buyer.
        await using var _ = await _lock.AcquireAsync($"show-status-change:{request.ShowId}", ct);

        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền đổi hình thức event này.");

        if (show.Status is not (LoungeShowStatus.Published or LoungeShowStatus.Ongoing))
            throw new DomainException("Chỉ có thể đổi hình thức event đã Published hoặc đang diễn ra.");

        if (show.Format != LoungeShowFormat.Offline || request.NewFormat != LoungeShowFormat.Online)
            throw new DomainException("Chỉ hỗ trợ đổi hình thức từ Offline sang Online sau khi đã bán vé.");

        show.Format = LoungeShowFormat.Online;
        showRepo.Update(show);

        var ticketRepo = _uow.Repository<Ticket, Guid>();
        var physicalTickets = await ticketRepo.FindAsync(
            t => t.ShowId == show.Id
                && t.Status == TicketStatus.Confirmed
                && t.Tier.AccessType == AccessType.Physical, ct);

        if (physicalTickets.Count > 0)
        {
            var priceIds = physicalTickets.Select(t => t.PriceId).Distinct().ToList();
            var prices = await _uow.Repository<TicketPrice, int>().FindAsync(p => priceIds.Contains(p.Id), ct);
            var priceById = prices.ToDictionary(p => p.Id, p => p.Price);

            var refundRepo = _uow.Repository<RefundRequest, int>();
            var payers = await TicketRefundRecipients.PayersAsync(_uow, physicalTickets, ct);

            foreach (var ticket in physicalTickets)
            {
                ticket.Status = TicketStatus.Cancelled;
                ticketRepo.Update(ticket);

                if (ticket.PaymentId is null) continue;

                await TicketRefundRecipients.NotifyOriginalBuyerAsync(_notifications, ticket, payers,
                    NotificationType.EventFormatChanged, show.Name, show.Id, "buổi diễn chuyển sang online", ct);

                refundRepo.Add(new RefundRequest
                {
                    PaymentId = ticket.PaymentId.Value,
                    RequestedBy = TicketRefundRecipients.RefundedTo(ticket, payers),
                    Reason = "Event chuyển từ Offline sang Online — hoàn 100% vé vật lý (D13)",
                    AmountRequested = priceById.GetValueOrDefault(ticket.PriceId),
                    RefundPercentage = 100m,
                    Status = RefundRequestStatus.Pending
                });

                if (ticket.BuyerId is int buyerId)
                    await _notifications.NotifyAsync(
                        buyerId,
                        NotificationType.EventFormatChanged,
                        "Event đã chuyển sang hình thức Online",
                        $"\"{show.Name}\" đã chuyển từ trực tiếp sang online. Vé vật lý của bạn đã được " +
                        "hủy và tự động tạo yêu cầu hoàn 100% tiền vé." + (TicketRefundRecipients.WasTransferred(ticket, payers) ? TicketRefundRecipients.TransferredHolderNote : ""),
                        referenceType: "show",
                        referenceId: show.Id.ToString(),
                        ct: ct);
            }
        }

        // MLACP-390: khong con khan gia tai cho — don F&B chua phuc vu gan voi buoi dien nay bi huy, tien tra truoc
        // hoan 100%. Mon da mang ra (Served) la hang da giao, phong tra van thu. Cung thu tu khoa voi ShowCancellation:
        // show-status-change (dang giu o tren) roi moi toi fnb-order:{id}.
        await FnbOrderCancellation.CancelOpenOrdersAsync(
            _uow, _notifications, _lock, o => o.ShowId == show.Id, servedToo: false, "buổi diễn chuyển sang online", ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
