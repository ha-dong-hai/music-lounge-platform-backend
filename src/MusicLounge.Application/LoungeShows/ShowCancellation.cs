using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.FnbOrders;
using MusicLounge.Application.Tickets;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows;

/// <summary>
/// Huỷ một buổi diễn: hoàn 100% cho MỌI vé đã xác nhận (không riêng vé vào cửa như đổi hình thức — vé livestream cũng
/// mất giá trị khi buổi diễn không còn), báo từng người giữ vé và người mua ban đầu của vé đã chuyển nhượng. Đơn F&B
/// gắn với buổi diễn (MLACP-380) cũng được huỷ theo cùng đường này — xem <see cref="CancelFnbOrdersAsync"/>.
///
/// <para>MLACP-373: tách khỏi <c>CancelLoungeShowCommandHandler</c> để nền tảng đi đúng đường này khi phòng trà bị khoá
/// hoặc tạm khoá (<c>ApplyDuePenaltiesJob</c>) — handler đòi người gọi là chủ phòng trà hoặc Admin, còn job thì không
/// có người gọi nào. Nơi gọi tự kiểm quyền và trạng thái buổi diễn, giữ khoá <c>show-status-change</c>, và tự lưu.</para>
/// </summary>
public static class ShowCancellation
{
    /// <summary>
    /// Lý do cho người mua khi nền tảng huỷ vì phòng trà bị khoá — trung tính như
    /// <see cref="Common.VenueLifecycle.TradingPausedForBuyers"/>: lý do khoá là chuyện giữa phòng trà với nền tảng.
    /// </summary>
    public const string VenueStoppedTrading = "phòng trà ngừng hoạt động trên nền tảng";

    /// <param name="WalkInTickets">Vé bán tại quầy (không có tài khoản): nền tảng không báo được người mua.</param>
    /// <param name="FnbOrders">MLACP-380: đơn F&amp;B gắn với buổi diễn đã bị huỷ theo (không tính đơn đã đóng —
    /// <see cref="CancelFnbOrdersAsync"/>).</param>
    public sealed record Outcome(int Shows, int WalkInTickets, int FnbOrders = 0)
    {
        public static readonly Outcome None = new(0, 0, 0);

        public static Outcome operator +(Outcome a, Outcome b)
            => new(a.Shows + b.Shows, a.WalkInTickets + b.WalkInTickets, a.FnbOrders + b.FnbOrders);
    }

    /// <param name="lock">MLACP-380: mỗi đơn F&amp;B bị đụng tới phải khoá đúng key <c>fnb-order:{id}</c> —
    /// cùng khoá với lúc khách trả tiền/nhân viên đổi trạng thái/IPN VNPay, tránh một đơn vừa bị huỷ ở đây vừa được
    /// xử lý ở một trong ba đường đó cùng lúc.</param>
    /// <param name="why">Null khi chính chủ phòng trà huỷ — nội dung báo giữ nguyên như trước.</param>
    public static async Task<Outcome> CancelAsync(
        IUnitOfWork uow, INotificationService notifications, IAsyncKeyedLock @lock, LoungeShow show, string? why,
        CancellationToken ct)
    {
        show.Status = LoungeShowStatus.Cancelled;
        uow.Repository<LoungeShow, int>().Update(show);

        var because = why is null ? "" : $" vì {why}";

        var ticketOutcome = await CancelTicketsAsync(uow, notifications, show, because, ct);
        var fnbOrders = await CancelFnbOrdersAsync(uow, notifications, @lock, show, because, ct);

        return ticketOutcome with { FnbOrders = fnbOrders };
    }

    private static async Task<Outcome> CancelTicketsAsync(
        IUnitOfWork uow, INotificationService notifications, LoungeShow show, string because, CancellationToken ct)
    {
        var ticketRepo = uow.Repository<Ticket, Guid>();
        var confirmedTickets = await ticketRepo.FindAsync(
            t => t.ShowId == show.Id && t.Status == TicketStatus.Confirmed, ct);
        if (confirmedTickets.Count == 0)
            return new Outcome(1, 0);

        var priceIds = confirmedTickets.Select(t => t.PriceId).Distinct().ToList();
        var prices = await uow.Repository<TicketPrice, int>().FindAsync(p => priceIds.Contains(p.Id), ct);
        var priceById = prices.ToDictionary(p => p.Id, p => p.Price);

        var refundRepo = uow.Repository<RefundRequest, int>();
        var payers = await TicketRefundRecipients.PayersAsync(uow, confirmedTickets, ct);

        foreach (var ticket in confirmedTickets)
        {
            ticket.Status = TicketStatus.Cancelled;
            ticketRepo.Update(ticket);

            if (ticket.PaymentId is null) continue;

            await TicketRefundRecipients.NotifyOriginalBuyerAsync(notifications, ticket, payers,
                NotificationType.EventCancelled, show.Name, show.Id, $"buổi diễn bị huỷ{because}", ct);

            refundRepo.Add(new RefundRequest
            {
                PaymentId = ticket.PaymentId.Value,
                RequestedBy = TicketRefundRecipients.RefundedTo(ticket, payers),
                Reason = $"Event bị hủy{because} — hoàn 100% tiền vé",
                AmountRequested = priceById.GetValueOrDefault(ticket.PriceId),
                RefundPercentage = 100m,
                Status = RefundRequestStatus.Pending
            });

            if (ticket.BuyerId is int buyerId)
                await notifications.NotifyAsync(
                    buyerId,
                    NotificationType.EventCancelled,
                    "Event đã bị hủy",
                    $"\"{show.Name}\" đã bị hủy{because}. Vé của bạn đã được hủy và tự động tạo yêu cầu " +
                    "hoàn 100% tiền vé." + (TicketRefundRecipients.WasTransferred(ticket, payers) ? TicketRefundRecipients.TransferredHolderNote : ""),
                    referenceType: "show",
                    referenceId: show.Id.ToString(),
                    ct: ct);
        }

        return new Outcome(1, confirmedTickets.Count(t => t.BuyerId is null));
    }

    /// <summary>
    /// MLACP-380: trước task này, huỷ show không đụng gì tới các <see cref="FnbOrder"/> gắn với nó (ShowId) — đơn
    /// khách đã đặt/đã trả trước cho một buổi diễn không còn tổ chức treo nguyên, tiền trả trước (nếu có) không ai
    /// hoàn.
    ///
    /// <para>Đơn đã đóng (<see cref="FnbOrderStatus.Paid"/>) là giao dịch đã xong trước khi show bị huỷ — dù đóng
    /// bằng tiền mặt hay online — không đụng tới, giữ đúng ranh giới <c>UpdateFnbOrderStatusCommandHandler</c> đã
    /// đặt cho việc huỷ một đơn F&amp;B (MLACP-349/351: chỉ huỷ được đơn CHƯA đóng). Đơn chưa đóng mà đã có một giao
    /// dịch Gateway Confirmed (khách trả trước qua VNPay, bếp chưa kịp phục vụ xong) thì hoàn 100% — cùng logic đã
    /// có ở đó. Đơn chưa đóng và chưa có giao dịch nào thì huỷ thẳng, không có gì để hoàn.</para>
    /// </summary>
    private static async Task<int> CancelFnbOrdersAsync(
        IUnitOfWork uow, INotificationService notifications, IAsyncKeyedLock @lock, LoungeShow show, string because,
        CancellationToken ct)
    {
        var orderRepo = uow.Repository<FnbOrder, int>();
        var orders = await orderRepo.FindAsync(
            o => o.ShowId == show.Id && o.Status != FnbOrderStatus.Cancelled, ct);
        if (orders.Count == 0) return 0;

        var itemRepo = uow.Repository<OrderItem, int>();
        var paymentRepo = uow.Repository<Payment, int>();
        var refundRepo = uow.Repository<RefundRequest, int>();
        var affected = 0;

        foreach (var order in orders)
        {
            // Cung khoa voi InitiateFnbOrderPayment / UpdateFnbOrderStatus / ProcessFnbOrderPayment (IPN) —
            // khong thi mot don co the vua bi huy o day vua duoc xu ly o mot trong ba noi do cung luc.
            await using var _ = await @lock.AcquireAsync(FnbOrderPayments.LockKey(order.Id), ct);

            // Doc lai sau khi co khoa: don co the da doi trang thai (vi du da duoc danh dau Paid) giua luc
            // truy van o tren va luc lay duoc khoa nay.
            var current = await orderRepo.GetByIdAsync(order.Id, ct);
            if (current is null || current.Status is FnbOrderStatus.Paid or FnbOrderStatus.Cancelled) continue;

            var referenceId = current.Id.ToString();
            var gatewayPayment = (await paymentRepo.FindAsync(
                    p => p.ReferenceType == FnbOrderPayments.ReferenceType
                         && p.ReferenceId == referenceId
                         && p.Status == PaymentStatus.Confirmed
                         && p.Method == PaymentMethod.Gateway, ct))
                .FirstOrDefault();

            current.Status = FnbOrderStatus.Cancelled;
            orderRepo.Update(current);

            var items = await itemRepo.FindAsync(i => i.FnbOrderId == current.Id, ct);
            foreach (var item in items)
            {
                item.Cancelled = true;
                itemRepo.Update(item);
            }

            decimal? refundAmount = null;
            if (gatewayPayment is not null)
            {
                refundAmount = gatewayPayment.GrossAmount;
                refundRepo.Add(new RefundRequest
                {
                    PaymentId = gatewayPayment.Id,
                    RequestedBy = current.AudienceUserId ?? gatewayPayment.PayerId,
                    Reason = $"Buổi diễn bị huỷ{because} — đơn F&B #{current.Id} chưa phục vụ xong, hoàn 100%",
                    AmountRequested = gatewayPayment.GrossAmount,
                    RefundPercentage = 100m,
                    Status = RefundRequestStatus.Pending
                });
            }

            affected++;

            // Don nhan vien dat ho khach vang lai (khong tai khoan) khong bao duoc — giong quy uoc
            // NotifyAudienceAsync cua UpdateFnbOrderStatusCommandHandler.
            if (current.AudienceUserId is { } audienceUserId)
            {
                var (title, body) = refundAmount is { } amount
                    ? ("Đơn F&B đã bị hủy — bạn sẽ được hoàn tiền",
                       $"Đơn #{current.Id} của bạn đã bị hủy vì buổi diễn bị huỷ{because}. Chúng tôi đã tự động " +
                       $"tạo yêu cầu hoàn 100% ({amount:N0}đ) về phương thức bạn đã thanh toán — bạn không cần " +
                       "làm gì thêm và sẽ được báo khi yêu cầu được xử lý.")
                    : ("Đơn F&B đã bị hủy",
                       $"Đơn #{current.Id} của bạn đã bị hủy vì buổi diễn bị huỷ{because}.");

                await notifications.NotifyAsync(
                    audienceUserId, NotificationType.FnbOrderUpdate, title, body,
                    referenceType: "fnbOrder", referenceId: current.Id.ToString(), ct: ct);
            }
        }

        return affected;
    }
}
