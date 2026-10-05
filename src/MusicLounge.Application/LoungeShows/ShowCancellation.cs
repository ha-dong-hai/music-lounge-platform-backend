using MusicLounge.Domain.ValueObjects;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.FnbOrders;
using MusicLounge.Application.Tickets;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows;

/// <summary>
/// Huỷ một buổi diễn: hoàn 100% cho MỌI vé đã xác nhận (không riêng vé vào cửa như đổi hình thức — vé livestream cũng
/// mất giá trị khi buổi diễn không còn), báo từng người giữ vé và người mua ban đầu của vé đã chuyển nhượng.
///
/// <para>ĐƠN ĐỒ UỐNG (MLACP-632, chủ dự án 04/10/2026): khi CHỦ PHÒNG TRÀ huỷ buổi diễn, đơn đồ uống KHÔNG bị huỷ theo —
/// phòng trà vẫn mở cửa, khách có thể vẫn ngồi uống; khách được báo và tự quyết theo luật thường ngày (tự huỷ khi quầy
/// chưa nhận, đã làm thì trao đổi với nhân viên). Trước đây (MLACP-380) huỷ buổi là huỷ luôn mọi đơn chưa đóng, kể cả
/// món đã mang ra bàn. Khi NỀN TẢNG huỷ vì phòng trà bị khoá/tạm khoá thì vẫn huỷ đơn như cũ — phòng trà không còn
/// được giao dịch trên hệ thống, và tiền khách trả trước phải được hoàn.</para>
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
    // MLACP-489: SongNgu thay cho const string — cụm này ghép vào thông báo gửi khán giả và chủ phòng trà.
    public static readonly SongNgu VenueStoppedTrading = new(
        "phòng trà ngừng hoạt động trên nền tảng",
        "the music lounge stopped operating on the platform");

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
    /// <param name="cancelFnbOrders">MLACP-632: true chỉ khi nền tảng huỷ vì phòng trà ngừng giao dịch. Chủ phòng trà
    /// huỷ buổi thì false — đơn đồ uống giữ nguyên, khách chỉ được báo.</param>
    public static async Task<Outcome> CancelAsync(
        IUnitOfWork uow, INotificationService notifications, IAsyncKeyedLock @lock, LoungeShow show, SongNgu? why,
        bool cancelFnbOrders, CancellationToken ct)
    {
        show.Status = LoungeShowStatus.Cancelled;
        uow.Repository<LoungeShow, Guid>().Update(show);

        var because = why is null ? SongNgu.Rong : new SongNgu($" vì {why.Vi}", $" because {why.En}");
        // Cụm "buổi diễn bị huỷ…" dùng chung cho người mua gốc của vé đã chuyển nhượng và cho đơn F&B đi kèm.
        var showCancelled = new SongNgu($"buổi diễn bị huỷ{because.Vi}", $"the show was cancelled{because.En}");

        var ticketOutcome = await CancelTicketsAsync(uow, notifications, show, because, showCancelled, ct);
        if (!cancelFnbOrders)
        {
            await NotifyOpenFnbOrdersKeptAsync(uow, notifications, show,
                new SongNgu($"\"{show.Name}\" đã bị huỷ.", $"\"{show.Name}\" has been cancelled."), ct);
            return ticketOutcome;
        }
        var fnbOrders = await CancelFnbOrdersAsync(uow, notifications, @lock, show, showCancelled, ct);

        return ticketOutcome with { FnbOrders = fnbOrders };
    }

    private static async Task<Outcome> CancelTicketsAsync(
        IUnitOfWork uow, INotificationService notifications, LoungeShow show, SongNgu because, SongNgu showCancelled,
        CancellationToken ct)
    {
        var ticketRepo = uow.Repository<Ticket, Guid>();
        var confirmedTickets = await ticketRepo.FindAsync(
            t => t.ShowId == show.Id && t.Status == TicketStatus.Confirmed, ct);
        if (confirmedTickets.Count == 0)
            return new Outcome(1, 0);

        var priceIds = confirmedTickets.Select(t => t.PriceId).Distinct().ToList();
        var prices = await uow.Repository<TicketPrice, Guid>().FindAsync(p => priceIds.Contains(p.Id), ct);
        var priceById = prices.ToDictionary(p => p.Id, p => p.Price);

        var refundRepo = uow.Repository<RefundRequest, Guid>();
        var payers = await TicketRefundRecipients.PayersAsync(uow, confirmedTickets, ct);

        foreach (var ticket in confirmedTickets)
        {
            ticket.Status = TicketStatus.Cancelled;
            ticketRepo.Update(ticket);

            if (ticket.PaymentId is null) continue;

            await TicketRefundRecipients.NotifyOriginalBuyerAsync(notifications, ticket, payers,
                NotificationType.EventCancelled, show.Name, show.Id, showCancelled, ct);

            refundRepo.Add(new RefundRequest
            {
                PaymentId = ticket.PaymentId.Value,
                RequestedBy = TicketRefundRecipients.RefundedTo(ticket, payers),
                // MLACP-675: lý do này hiện cho khán giả (trang Hoàn tiền, thông báo duyệt hoàn) — viết bằng ngôn ngữ nghiệp
                // vụ, không phải "Event bị hủy".
                Reason = $"{RefundReasonShowCancelled}{because.Vi} — hoàn 100% tiền vé",
                AmountRequested = priceById.GetValueOrDefault(ticket.PriceId),
                RefundPercentage = 100m,
                Status = RefundRequestStatus.Pending
            });
        }

        // MLACP-675: MỘT thông báo cho mỗi người giữ vé, không phải một thông báo cho mỗi vé — trước đây người mua 4 vé nhận 4
        // thông báo giống hệt nhau. Câu cũ "đã tự động tạo yêu cầu hoàn 100%" là ngôn ngữ của hệ thống: không nói hoàn bao
        // nhiêu, về đâu, có phải làm gì không. Nay nói tên buổi, giờ diễn, số vé, số tiền, về đâu, và rằng không cần làm gì.
        var gioDien = VietnamTime.Format(show.ScheduledStart, "HH:mm dd/MM/yyyy");
        foreach (var nhom in confirmedTickets.Where(t => t.PaymentId is not null && t.BuyerId is not null).GroupBy(t => t.BuyerId!.Value))
        {
            var cuaMinh = nhom.Where(t => !TicketRefundRecipients.WasTransferred(t, payers)).ToList();
            var duocChuyen = nhom.Count() - cuaMinh.Count;
            var tong = cuaMinh.Sum(t => priceById.GetValueOrDefault(t.PriceId));

            var hoanVi = cuaMinh.Count == 0 ? ""
                : $" {cuaMinh.Count} vé của bạn được hoàn 100% ({VietnamMoney.Format(tong)}) về đúng phương thức bạn đã thanh " +
                  "toán — bạn không cần làm gì thêm. Theo dõi tiến độ ở Vé của tôi → Hoàn tiền.";
            var hoanEn = cuaMinh.Count == 0 ? ""
                : $" Your {cuaMinh.Count} ticket(s) will be refunded in full ({tong:N0} VND) to the payment method you used — you " +
                  "do not need to do anything. Track it under My tickets → Refunds.";
            var tenTieuDe = new SongNgu($"Buổi hòa nhạc \"{show.Name}\" đã bị huỷ", $"\"{show.Name}\" has been cancelled");
            await notifications.NotifyAsync(
                nhom.Key,
                NotificationType.EventCancelled,
                tenTieuDe,
                new SongNgu(
                    $"Buổi hòa nhạc \"{show.Name}\" lúc {gioDien} đã bị huỷ{because.Vi}.{hoanVi}" +
                    (duocChuyen > 0 ? TicketRefundRecipients.TransferredHolderNote : ""),
                    $"\"{show.Name}\" at {gioDien} (Vietnam time) has been cancelled{because.En}.{hoanEn}" +
                    (duocChuyen > 0 ? TicketRefundRecipients.TransferredHolderNoteEn : "")),
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);
        }

        return new Outcome(1, confirmedTickets.Count(t => t.BuyerId is null));
    }

    /// <summary>MLACP-675. Đầu câu lý do của yêu cầu hoàn tạo ra khi buổi diễn bị huỷ — thông báo duyệt hoàn nhận ra nguồn
    /// gốc của khoản hoàn qua chính câu này (RefundRequest chưa có cột nguồn gốc; đường nâng cấp: thêm cột Origin).</summary>
    public const string RefundReasonShowCancelled = "Buổi hòa nhạc bị huỷ";

    /// <summary>
    /// MLACP-380: trước task này, huỷ show không đụng gì tới các <see cref="FnbOrder"/> gắn với nó (ShowId) — đơn
    /// khách đã đặt/đã trả trước cho một buổi diễn không còn tổ chức treo nguyên, tiền trả trước (nếu có) không ai
    /// hoàn. Huỷ mọi đơn chưa đóng, kể cả đơn đã phục vụ, theo luật chung ở <see cref="FnbOrderCancellation"/>
    /// (MLACP-390 tách ra để đường chuyển sang online dùng lại).
    /// </summary>
    /// <summary>
    /// MLACP-632 — buổi diễn bị chủ phòng trà huỷ hoặc chuyển sang chỉ phát trực tuyến: báo khách có đơn đồ uống còn mở
    /// rằng đơn VẪN GIỮ và họ tự quyết. Dùng chung cho ChangeLoungeShowFormatCommandHandler.
    /// Đơn của khách đặt qua app không mang ShowId (app không gắn đơn với buổi — xem CreateFnbOrderCommandHandler),
    /// nên ngoài đơn gắn đúng buổi này còn tính đơn còn mở ở CÙNG phòng trà của người có vé buổi này.
    /// </summary>
    /// <param name="what">Câu mở đầu nói chuyện gì đã xảy ra với buổi diễn, ví dụ "\"Tên buổi\" đã bị huỷ."</param>
    internal static async Task NotifyOpenFnbOrdersKeptAsync(
        IUnitOfWork uow, INotificationService notifications, LoungeShow show, SongNgu what, CancellationToken ct)
    {
        var holderIds = (await uow.Repository<Ticket, Guid>().FindAsync(t => t.ShowId == show.Id && t.BuyerId != null, ct))
            .Select(t => t.BuyerId!.Value).Distinct().ToList();
        var open = new[] { FnbOrderStatus.Pending, FnbOrderStatus.Preparing, FnbOrderStatus.Served };
        var orders = await uow.Repository<FnbOrder, Guid>().FindAsync(
            o => o.AudienceUserId != null && open.Contains(o.Status)
                 && (o.ShowId == show.Id || (o.LoungeId == show.LoungeId && holderIds.Contains(o.AudienceUserId!.Value))), ct);

        foreach (var o in orders)
        {
            var (vi, en) = o.Status == FnbOrderStatus.Pending
                ? ("Quầy chưa nhận đơn — nếu không cần nữa, bạn tự huỷ được trong mục Đơn của tôi (đã trả trước thì được hoàn 100%).",
                   "The bar has not started it yet — if you no longer want it, you can cancel it under My orders (prepaid orders are refunded in full).")
                : ("Quầy đã bắt đầu làm — nếu muốn đổi ý, hãy trao đổi với nhân viên phục vụ.",
                   "The bar has already started on it — if you want to change it, please talk to the staff.");
            await notifications.NotifyAsync(
                o.AudienceUserId!.Value, NotificationType.FnbOrderUpdate,
                new SongNgu("Đơn đồ uống của bạn vẫn được giữ", "Your food & drink order is kept"),
                new SongNgu(
                    $"{what.Vi} Đơn đồ uống #{o.Id} của bạn vẫn được giữ. {vi}",
                    $"{what.En} Your food & drink order #{o.Id} is kept. {en}"),
                referenceType: "fnb_order", referenceId: o.Id.ToString(), ct: ct);
        }
    }

    private static Task<int> CancelFnbOrdersAsync(
        IUnitOfWork uow, INotificationService notifications, IAsyncKeyedLock @lock, LoungeShow show,
        SongNgu showCancelled, CancellationToken ct)
        => FnbOrderCancellation.CancelOpenOrdersAsync(
            uow, notifications, @lock, o => o.ShowId == show.Id, servedToo: true, showCancelled, ct);
}
