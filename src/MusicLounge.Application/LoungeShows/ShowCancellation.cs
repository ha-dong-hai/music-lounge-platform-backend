using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows;

/// <summary>
/// Huỷ một buổi diễn: hoàn 100% cho MỌI vé đã xác nhận (không riêng vé vào cửa như đổi hình thức — vé livestream cũng
/// mất giá trị khi buổi diễn không còn), báo từng người giữ vé và người mua ban đầu của vé đã chuyển nhượng.
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
    public sealed record Outcome(int Shows, int WalkInTickets)
    {
        public static readonly Outcome None = new(0, 0);

        public static Outcome operator +(Outcome a, Outcome b)
            => new(a.Shows + b.Shows, a.WalkInTickets + b.WalkInTickets);
    }

    /// <param name="why">Null khi chính chủ phòng trà huỷ — nội dung báo giữ nguyên như trước.</param>
    public static async Task<Outcome> CancelAsync(
        IUnitOfWork uow, INotificationService notifications, LoungeShow show, string? why, CancellationToken ct)
    {
        show.Status = LoungeShowStatus.Cancelled;
        uow.Repository<LoungeShow, int>().Update(show);

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
        var because = why is null ? "" : $" vì {why}";

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
}
