using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.Commands.ProcessVnPayCallback;

internal sealed class ProcessVnPayCallbackCommandHandler
    : IRequestHandler<ProcessVnPayCallbackCommand, VnPayIpnOutcome>
{
    private readonly IUnitOfWork _uow;
    private readonly IVnPayService _vnPay;
    private readonly ILoungeShowRepository _showRepo;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ITicketRepository _ticketRepo;
    private readonly IPublisher _publisher;
    private readonly IAsyncKeyedLock _lock;
    private readonly INotificationService _notifications;
    private readonly ILogger<ProcessVnPayCallbackCommandHandler> _logger;

    public ProcessVnPayCallbackCommandHandler(
        IUnitOfWork uow,
        IVnPayService vnPay,
        ILoungeShowRepository showRepo,
        ILivestreamRepository livestreamRepo,
        ITicketRepository ticketRepo,
        IPublisher publisher,
        IAsyncKeyedLock @lock,
        INotificationService notifications,
        ILogger<ProcessVnPayCallbackCommandHandler> logger)
    {
        _uow = uow;
        _vnPay = vnPay;
        _showRepo = showRepo;
        _livestreamRepo = livestreamRepo;
        _ticketRepo = ticketRepo;
        _publisher = publisher;
        _lock = @lock;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<VnPayIpnOutcome> Handle(ProcessVnPayCallbackCommand request, CancellationToken ct)
    {
        var result = _vnPay.VerifyCallback(request.QueryParams);
        request.QueryParams.TryGetValue("vnp_TxnRef", out var txnRefForLogging);

        // Reject tampered/forged callbacks immediately — do NOT modify any data.
        if (!result.IsSignatureValid)
        {
            _logger.LogWarning(
                "VNPay ticket callback rejected — invalid signature: TxnRef={TxnRef} at {At}",
                txnRefForLogging, DateTimeOffset.UtcNow);
            return VnPayIpnOutcome.InvalidSignature;
        }

        var txnRef = txnRefForLogging;

        // Same VNPay retry-storm hazard as donations/subscriptions — without this lock, 2
        // near-simultaneous callbacks both read Status==Pending before either commits, both
        // confirm/cancel the same tickets twice.
        await using var _ = await _lock.AcquireAsync($"vnpay-ticket:{txnRef}", ct);

        var paymentRepo = _uow.Repository<Payment, int>();
        var payments = await paymentRepo.FindAsync(p => p.OrderId == txnRef, ct);

        var payment = payments.FirstOrDefault();
        if (payment is null)
        {
            _logger.LogWarning(
                "VNPay ticket callback rejected — no Payment found for TxnRef={TxnRef} at {At}",
                txnRef, DateTimeOffset.UtcNow);
            return VnPayIpnOutcome.OrderNotFound;
        }

        // Idempotency: VNPay có thể gọi lại nhiều lần (browser redirect + IPN cùng trỏ vào lệnh
        // này). Nếu đã xử lý rồi thì bỏ qua để tránh chuyển trạng thái 2 lần.
        if (payment.Status != PaymentStatus.Pending)
        {
            // MLACP-334. Truoc day ca hai tinh huong duoi day deu tra ve cung mot ket qua, nen cai
            // thu hai — tien that da thu — bien mat sau mot dong log muc Information.
            if (result.IsSuccess && payment.Status != PaymentStatus.Confirmed)
            {
                // MLACP-382: callback lap lai (trinh duyet quay ve + IPN, hoac VNPay goi lai) cua mot giao dich
                // da duoc ghi nhan la "tien ve cho ve cua buoi dien da huy" (RecordNotIssuedAsync) — su co da
                // duoc bao va yeu cau hoan da tao, khong bao lan nua. Chi nhanh do ghi TransactionId len mot
                // thanh toan Failed: nhanh that bai o duoi va CancelAbandonedPaymentsJob deu khong ghi.
                if (payment.Status == PaymentStatus.Failed
                    && !string.IsNullOrEmpty(payment.TransactionId)
                    && payment.TransactionId == result.TransactionId)
                    return VnPayIpnOutcome.ConfirmedTooLate;

                // MLACP-385: thanh toan da bi dong truoc khi tien ve — khach tu huy ve dang cho (CancelTicket) hoac
                // CancelAbandonedPaymentsJob dong vi qua han — ma chua ve nao duoc cap. Truoc day chi bao Admin: tien
                // cua khach treo toi khi co nguoi xu ly tay. Van KHONG tu cap lai ve (ly do MLACP-334: cho co the da ban
                // cho nguoi khac) — nhung don khong duoc dap ung thi khong giu tien cua khach (Ticketmaster: "can't be
                // fulfilled, your card won't be charged"): tu tao yeu cau hoan 100%, Admin van duoc bao va van co the
                // tu choi yeu cau de cap lai ve bang tay neu khach muon.
                if (payment.Status == PaymentStatus.Failed
                    && string.IsNullOrEmpty(payment.TransactionId)
                    && result.Amount == payment.GrossAmount)
                {
                    var closedTickets = await _uow.Repository<Ticket, Guid>().FindAsync(t => t.PaymentId == payment.Id, ct);
                    if (closedTickets.Count > 0
                        && closedTickets.All(t => t.Status is TicketStatus.Cancelled or TicketStatus.Pending)
                        && await _uow.Repository<LoungeShow, int>().GetByIdAsync(closedTickets[0].ShowId, ct) is { } closedShow)
                        return await RecordNotIssuedAsync(
                            payment, closedTickets, closedShow, NotIssued.OrderClosed, result, txnRef, ct);
                }

                await PaymentIncident.RecordConfirmedTooLateAsync(
                    _uow, _notifications, _logger, "mua ve", txnRef, result.Amount,
                    "payment", payment.Id.ToString(), ct);
                return VnPayIpnOutcome.ConfirmedTooLate;
            }

            _logger.LogInformation(
                "VNPay ticket callback replay — PaymentId={PaymentId} TxnRef={TxnRef} already {Status} at {At}",
                payment.Id, txnRef, payment.Status, DateTimeOffset.UtcNow);
            return VnPayIpnOutcome.AlreadyProcessed;
        }

        // Signature only proves VNPay sent this callback, not that it's for the amount we asked
        // for — fail closed on mismatch instead of confirming tickets for an unexpected amount.
        if (result.IsSuccess && result.Amount != payment.GrossAmount)
        {
            _logger.LogWarning(
                "VNPay ticket callback rejected — amount mismatch: PaymentId={PaymentId} TxnRef={TxnRef} Expected={Expected} Received={Received} at {At}",
                payment.Id, txnRef, payment.GrossAmount, result.Amount, DateTimeOffset.UtcNow);
            return VnPayIpnOutcome.AmountMismatch;
        }

        var ticketRepo = _uow.Repository<Ticket, Guid>();
        var tickets = await ticketRepo.FindAsync(t => t.PaymentId == payment.Id, ct);

        // MLACP-382: cung khoa voi CancelLoungeShow / ApplyDuePenaltiesJob (ShowCancellation) — khong co no thi
        // buoi dien co the bi huy ngay giua luc doc trang thai o duoi va luc ve duoc xac nhan, va ShowCancellation
        // (chi doc ve Confirmed) bo sot dung nhung ve nay.
        await using var showLock = tickets.Count > 0
            ? await _lock.AcquireAsync($"show-status-change:{tickets[0].ShowId}", ct)
            : null;

        if (result.IsSuccess)
        {
            // MLACP-382: truoc day nhanh nay khong nhin buoi dien. Khach bam thanh toan (ve Pending), buoi dien bi
            // huy trong luc khach con tren trang VNPay — ShowCancellation chi hoan ve Confirmed nen bo qua ve nay —
            // roi tien ve: ve bi chuyen Confirmed cho mot buoi dien khong con to chuc, khong ai tao yeu cau hoan.
            if (tickets.Count > 0
                && await _uow.Repository<LoungeShow, int>().GetByIdAsync(tickets[0].ShowId, ct) is { } showNow)
            {
                if (showNow.Status == LoungeShowStatus.Cancelled)
                    return await RecordNotIssuedAsync(
                        payment, tickets, showNow, NotIssued.ShowCancelled, result, txnRef, ct);

                // MLACP-383: cung ke ho do voi ChangeLoungeShowFormat — no chi hoan ve vao cua da Confirmed, con ve
                // vao cua dang Pending luc buoi dien chuyen online thi den day moi co tien.
                var tierNow = await _uow.Repository<TicketTier, int>().GetByIdAsync(tickets[0].TierId, ct);
                if (tierNow is not null && !PhysicalAccess.IsOffered(showNow, tierNow.AccessType))
                    return await RecordNotIssuedAsync(
                        payment, tickets, showNow, NotIssued.WentOnline, result, txnRef, ct);
            }

            payment.Status = PaymentStatus.Confirmed;
            payment.TransactionId = result.TransactionId;
            payment.VnPayResponseCode = result.ResponseCode;
            payment.PaidAt = DateTimeOffset.UtcNow;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            paymentRepo.Update(payment);

            // QrCode: dinh danh khong the doan duoc — Guid ngau nhien, khong phai chuoi tang dan/
            // rut gon tu ID nao co the du doan.
            foreach (var ticket in tickets)
            {
                ticket.Status = TicketStatus.Confirmed;
                ticket.QrCode = Guid.NewGuid().ToString("N");
                ticketRepo.Update(ticket);
            }

            // Chi tiet ve theo AccessType cua tier — moi Payment chi thuoc 1 tier duy nhat
            // (PurchaseTicketCommandHandler tao tat ca ve tu cung 1 hold, cung 1 price/tier).
            var firstTicket = tickets.FirstOrDefault();
            if (firstTicket is not null)
            {
                var tier = await _uow.Repository<TicketTier, int>().GetByIdAsync(firstTicket.TierId, ct);
                if (tier?.AccessType == AccessType.Livestream)
                {
                    var livestream = await _livestreamRepo.GetByShowIdAsync(firstTicket.ShowId, ct);
                    if (livestream is not null)
                    {
                        foreach (var ticket in tickets)
                            _livestreamRepo.AddTicketDetail(new LivestreamTicketDetail
                            {
                                TicketId = ticket.Id,
                                LivestreamId = livestream.Id,
                                AccessToken = Guid.NewGuid().ToString("N")
                            });
                    }
                }
                else if (tier?.AccessType == AccessType.Physical)
                {
                    foreach (var ticket in tickets)
                        _ticketRepo.AddPhysicalDetail(new PhysicalTicketDetail { TicketId = ticket.Id });
                }
            }

            await _uow.SaveChangesAsync(ct);

            var ownerId = firstTicket is not null
                ? await _showRepo.GetLoungeOwnerIdAsync(firstTicket.ShowId, ct) ?? 0
                : 0;

            await _publisher.Publish(new TicketPaymentConfirmed(
                PaymentId: payment.Id,
                UserId: firstTicket?.BuyerId ?? 0,
                OwnerId: ownerId,
                TicketIds: tickets.Select(t => t.Id).ToArray(),
                LivestreamId: null,
                ShowId: firstTicket?.ShowId ?? 0), ct);

            _logger.LogInformation(
                "VNPay ticket callback confirmed: PaymentId={PaymentId} TxnRef={TxnRef} TicketCount={TicketCount} at {At}",
                payment.Id, txnRef, tickets.Count, DateTimeOffset.UtcNow);
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            payment.VnPayResponseCode = result.ResponseCode;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            paymentRepo.Update(payment);

            // Hold da duoc tieu (IsReleased) tu luc tao Payment o PurchaseTicketCommandHandler —
            // "giai phong cho" o day nghia la huy cac Ticket Pending, khong con tinh vao
            // GetReservedQuantitiesByPriceIdsAsync (chi dem Confirmed/Pending), tra lai suat cho
            // nguoi khac mua.
            foreach (var ticket in tickets)
            {
                ticket.Status = TicketStatus.Cancelled;
                ticketRepo.Update(ticket);
            }

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "VNPay ticket callback failed at gateway: PaymentId={PaymentId} TxnRef={TxnRef} ResponseCode={ResponseCode} at {At}",
                payment.Id, txnRef, result.ResponseCode, DateTimeOffset.UtcNow);
        }

        return result.IsSuccess ? VnPayIpnOutcome.Confirmed : VnPayIpnOutcome.RecordedAsFailed;
    }

    /// <summary>
    /// MLACP-382. Tiền đã rời tài khoản khách cho vé của một buổi diễn đã bị huỷ trong lúc khách còn đang trả.
    ///
    /// <para>Không cấp vé — cấp là bán vé cho một buổi diễn không còn tổ chức. Nhưng cũng không để khoản tiền biến
    /// mất: làm đúng mẫu F&amp;B đã chốt (<c>ProcessFnbOrderPayment.RecordNotAppliedAsync</c>, MLACP-351). Giữ lại
    /// đúng dữ liệu VNPay báo — mã giao dịch chính là thứ lệnh hoàn qua VNPay cần — và <c>Failed</c> nghĩa là
    /// "không áp vào vé". Tự tạo yêu cầu hoàn 100%: buổi diễn bị huỷ thì mọi người mua của nó được hoàn đủ
    /// (<c>ShowCancellation</c>), người trả tiền muộn vài phút không phải ngoại lệ. <c>ProcessRefundRequest</c> không
    /// đảo bút toán cho thanh toán <c>Failed</c>, vì bút toán mua chỉ được ghi khi vé được xác nhận.</para>
    /// </summary>
    private async Task<VnPayIpnOutcome> RecordNotIssuedAsync(
        Payment payment, IReadOnlyList<Ticket> tickets, LoungeShow show, NotIssued why, VnPayCallbackResult result,
        string? txnRef, CancellationToken ct)
    {
        var (refundReason, noticeType, noticeTitle, whatHappened, incidentLabel) = why switch
        {
            NotIssued.WentOnline => (
                $"Tiền về cho vé vào cửa của buổi diễn #{show.Id} đã chuyển sang online — hoàn 100% (D13)",
                NotificationType.EventFormatChanged,
                "Buổi diễn đã chuyển sang online — bạn sẽ được hoàn tiền",
                "buổi diễn đã chuyển sang hình thức online trong lúc bạn đang thanh toán nên vé vào cửa không được cấp",
                "ve vao cua cua buoi dien da chuyen online"),
            NotIssued.OrderClosed => (
                $"Tiền về sau khi đơn vé của buổi diễn #{show.Id} đã đóng (khách huỷ hoặc quá hạn thanh toán) — vé không được cấp, hoàn 100%",
                NotificationType.RefundUpdate,
                "Vé không được cấp — bạn sẽ được hoàn tiền",
                "đơn vé này đã được huỷ hoặc đã hết hạn thanh toán trước khi tiền về nên vé không được cấp",
                "mua ve (don da dong truoc khi tien ve)"),
            _ => (
                $"Tiền về cho vé của buổi diễn #{show.Id} đã bị huỷ trước đó — hoàn 100%",
                NotificationType.EventCancelled,
                "Buổi diễn đã bị huỷ — bạn sẽ được hoàn tiền",
                "buổi diễn đã bị huỷ trong lúc bạn đang thanh toán nên vé không được cấp",
                "ve cua buoi dien da huy")
        };

        var now = DateTimeOffset.UtcNow;

        payment.Status = PaymentStatus.Failed;
        payment.TransactionId = result.TransactionId;
        payment.VnPayResponseCode = result.ResponseCode;
        payment.PaidAt = now;
        payment.UpdatedAt = now;
        _uow.Repository<Payment, int>().Update(payment);

        var ticketRepo = _uow.Repository<Ticket, Guid>();
        foreach (var ticket in tickets)
        {
            ticket.Status = TicketStatus.Cancelled;
            ticketRepo.Update(ticket);
        }

        // Luc bam thanh toan ve chua the duoc chuyen nhuong, nen nguoi giu ve chinh la nguoi tra tien — dung
        // BuyerId khi thanh toan thieu PayerId.
        var payerId = payment.PayerId ?? tickets.Select(t => t.BuyerId).FirstOrDefault(b => b is not null);

        _uow.Repository<RefundRequest, int>().Add(new RefundRequest
        {
            PaymentId = payment.Id,
            RequestedBy = payerId,
            Reason = refundReason,
            AmountRequested = payment.GrossAmount,
            RefundPercentage = 100m,
            Status = RefundRequestStatus.Pending
        });

        if (payerId is int buyerId)
            await _notifications.NotifyAsync(
                buyerId,
                noticeType,
                noticeTitle,
                $"Giao dịch {payment.GrossAmount:N0}đ (mã {result.TransactionId}) cho vé \"{show.Name}\" đã bị trừ " +
                $"tiền, nhưng {whatHappened}. Chúng tôi đã " +
                "tự động tạo yêu cầu hoàn 100% khoản này về phương thức bạn đã thanh toán — bạn không cần làm gì " +
                "thêm và sẽ được báo khi yêu cầu được xử lý.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);

        // Luu ban ghi thanh toan, ve, yeu cau hoan va thong bao cho khach; PaymentIncident tu luu phan cua no.
        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "VNPay ticket payment arrived but tickets were not issued ({Why}) — full refund queued: " +
            "PaymentId={PaymentId} TxnRef={TxnRef} ShowId={ShowId} Amount={Amount} at {At}",
            why, payment.Id, txnRef, show.Id, payment.GrossAmount, now);

        await PaymentIncident.RecordConfirmedTooLateAsync(
            _uow, _notifications, _logger, incidentLabel, txnRef, result.Amount,
            "payment", payment.Id.ToString(), ct);

        return VnPayIpnOutcome.ConfirmedTooLate;
    }

    /// <summary>Vì sao vé không được cấp dù VNPay đã thu tiền.</summary>
    private enum NotIssued
    {
        /// <summary>MLACP-382: buổi diễn bị huỷ trong lúc khách đang trả tiền.</summary>
        ShowCancelled,

        /// <summary>MLACP-383: vé vào cửa, buổi diễn chuyển sang online trong lúc khách đang trả tiền — D13 hoàn 100%.</summary>
        WentOnline,

        /// <summary>MLACP-385: thanh toán đã bị đóng (khách tự huỷ vé đang chờ, hoặc quá hạn thanh toán) trước khi tiền
        /// về, và chưa vé nào được cấp. Không tự cấp lại vé (MLACP-334) — hoàn 100%.</summary>
        OrderClosed
    }
}
