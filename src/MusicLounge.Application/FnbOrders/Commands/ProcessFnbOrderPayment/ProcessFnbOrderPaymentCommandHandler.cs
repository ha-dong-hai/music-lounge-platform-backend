using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbOrders.Commands.ProcessFnbOrderPayment;

internal sealed class ProcessFnbOrderPaymentCommandHandler
    : IRequestHandler<ProcessFnbOrderPaymentCommand, VnPayIpnOutcome>
{
    private readonly IUnitOfWork _uow;
    private readonly IVnPayService _vnPay;
    private readonly ILedgerService _ledger;
    private readonly INotificationService _notifications;
    private readonly IAsyncKeyedLock _lock;
    private readonly ILogger<ProcessFnbOrderPaymentCommandHandler> _logger;

    public ProcessFnbOrderPaymentCommandHandler(
        IUnitOfWork uow, IVnPayService vnPay, ILedgerService ledger, INotificationService notifications,
        IAsyncKeyedLock @lock, ILogger<ProcessFnbOrderPaymentCommandHandler> logger)
    {
        _uow = uow;
        _vnPay = vnPay;
        _ledger = ledger;
        _notifications = notifications;
        _lock = @lock;
        _logger = logger;
    }

    public async Task<VnPayIpnOutcome> Handle(ProcessFnbOrderPaymentCommand request, CancellationToken ct)
    {
        var callbackResult = _vnPay.VerifyCallback(request.QueryParams);

        if (!callbackResult.IsSignatureValid)
        {
            request.QueryParams.TryGetValue("vnp_TxnRef", out var rejectedTxnRef);
            _logger.LogWarning(
                "VNPay F&B callback rejected: invalid signature. TxnRef={TxnRef}", rejectedTxnRef);
            return VnPayIpnOutcome.InvalidSignature;
        }

        request.QueryParams.TryGetValue("vnp_TxnRef", out var txnRef);

        // Same reasoning as ProcessDonationPaymentCommandHandler/ProcessVnPayCallbackCommandHandler:
        // VNPay retries the IPN callback if it doesn't get back the exact response it expects —
        // without this lock, 2 near-simultaneous callbacks for the SAME transaction could both apply
        // it. The per-ORDER lock below is a different guard: two DIFFERENT transactions for one order.
        await using var _ = await _lock.AcquireAsync($"vnpay-fnborder:{txnRef}", ct);

        var payments = await _uow.Repository<Payment, int>().FindAsync(p => p.OrderId == txnRef, ct);
        var payment = payments.FirstOrDefault();
        if (payment is null) return VnPayIpnOutcome.OrderNotFound;

        // Callback lap lai cua mot giao dich da ap vao don.
        if (payment.Status == PaymentStatus.Confirmed)
            return VnPayIpnOutcome.AlreadyProcessed;

        // Callback lap lai cua mot giao dich da duoc ghi nhan la "tien da thu nhung khong ap vao don"
        // (xem RecordNotAppliedAsync) — su co da duoc bao, khong bao lan nua.
        if (payment.Status == PaymentStatus.Failed
            && !string.IsNullOrEmpty(payment.TransactionId)
            && payment.TransactionId == callbackResult.TransactionId)
            return VnPayIpnOutcome.ConfirmedTooLate;

        if (!callbackResult.IsSuccess)
        {
            if (payment.Status != PaymentStatus.Pending)
                return VnPayIpnOutcome.AlreadyProcessed;

            payment.Status = PaymentStatus.Failed;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            _uow.Repository<Payment, int>().Update(payment);
            await _uow.SaveChangesAsync(ct);
            _logger.LogWarning(
                "VNPay F&B payment failed: PaymentId={PaymentId} ResponseCode={ResponseCode}",
                payment.Id, callbackResult.ResponseCode);
            return VnPayIpnOutcome.RecordedAsFailed;
        }

        if (callbackResult.Amount != payment.GrossAmount)
        {
            _logger.LogWarning(
                "VNPay F&B callback amount mismatch: PaymentId={PaymentId} Expected={Expected} Actual={Actual}",
                payment.Id, payment.GrossAmount, callbackResult.Amount);
            return VnPayIpnOutcome.AmountMismatch;
        }

        // MLACP-349. Khoa theo DON truoc khi doc don — cung khoa ma nhan vien dung khi thu tien mat
        // hay huy, va khach dung khi tao link thanh toan moi.
        var orderId = int.Parse(payment.ReferenceId);
        await using var orderLock = await _lock.AcquireAsync(FnbOrderPayments.LockKey(orderId), ct);

        var order = await _uow.Repository<FnbOrder, int>().GetByIdAsync(orderId, ct);
        if (order is null) return VnPayIpnOutcome.InternalError;

        // MLACP-349. Truoc day nhanh nay khong nhin DON: moi giao dich con Pending deu duoc ap, va don
        // bi dat Paid vo dieu kien. Nen mot don da tra (bang giao dich khac hoac tien mat) bi tru tien
        // lan hai va ghi so cai lan hai; mot don da HUY ma tien ve muon thi bi hoi sinh thanh Paid.
        // Tai lieu VNPay: "Viec kiem tra trang thai cua don hang giup he thong khong xu ly trung lap,
        // xu ly nhieu lan mot giao dich."
        var paidElsewhere = order.Status == FnbOrderStatus.Paid
            || await FnbOrderPayments.HasConfirmedPaymentAsync(_uow, order.Id, payment.Id, ct);

        if (order.Status == FnbOrderStatus.Cancelled || paidElsewhere)
            return await RecordNotAppliedAsync(payment, order, callbackResult, txnRef, paidElsewhere, ct);

        // Don con mo va chua tra: ap giao dich nay — ke ca khi no da bi job don dep danh Failed vi qua
        // han. Voi ve, xac nhan den muon bi tu choi vi cho ngoi co the da ban cho nguoi khac; mot don
        // F&B con mo thi khong co thu gi bi mat, va tu choi o day nghia la khach da tra tien ma don van
        // hien "chua thanh toan".
        var now = DateTimeOffset.UtcNow;

        // F&B is commission-free (same premise as UpdateFnbOrderStatusCommandHandler's cash Paid
        // path) — net equals gross, no platform/tax split.
        payment.NetAmount = payment.GrossAmount;
        payment.Status = PaymentStatus.Confirmed;
        payment.TransactionId = callbackResult.TransactionId;
        payment.VnPayResponseCode = callbackResult.ResponseCode;
        payment.PaidAt = now;
        payment.UpdatedAt = now;
        _uow.Repository<Payment, int>().Update(payment);

        order.PaymentMethod = PaymentMethod.Gateway;

        // MLACP-349: chi dong don khi bep da phuc vu xong. Truoc day IPN nhay thang Pending -> Paid,
        // ma sau Paid khong con buoc nao — bep khong chuyen duoc don tra truoc sang Preparing/Served.
        // Square cung vay: "You cannot set the fulfillment.state of any order fulfillment to COMPLETED
        // until after you call PayOrder" — tra tien va phuc vu la hai viec, don dong khi ca hai xong.
        var closesOrder = order.Status == FnbOrderStatus.Served;
        if (closesOrder)
            order.Status = FnbOrderStatus.Paid;
        _uow.Repository<FnbOrder, int>().Update(order);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(order.LoungeId, ct);
        if (lounge is not null)
        {
            await _ledger.WriteJournalAsync(
                Guid.NewGuid().ToString("N"),
                LedgerReferenceTypes.FnbOrder,
                order.Id.ToString(),
                paymentId: payment.Id,
                new LedgerLine[]
                {
                    new(AccountType.Gateway, null, payment.GrossAmount, IsDebit: true),
                    new(AccountType.User, lounge.OwnerId, payment.GrossAmount, IsDebit: false,
                        Description: $"Don F&B #{order.Id} - thanh toan qua VNPay")
                }, ct);
        }

        await _uow.SaveChangesAsync(ct);

        if (order.AudienceUserId is { } audienceUserId)
        {
            await _notifications.NotifyAsync(
                audienceUserId, NotificationType.FnbOrderUpdate,
                "Thanh toán F&B thành công",
                closesOrder
                    ? $"Đơn #{order.Id} của bạn đã thanh toán thành công {payment.GrossAmount:N0}đ."
                    : $"Đơn #{order.Id} của bạn đã thanh toán thành công {payment.GrossAmount:N0}đ. " +
                      "Phòng trà vẫn đang chuẩn bị món — bạn sẽ được báo khi món được phục vụ.",
                referenceType: "fnbOrder", referenceId: order.Id.ToString(), ct: ct);
        }

        _logger.LogInformation(
            "F&B order payment confirmed: OrderId={OrderId} PaymentId={PaymentId} Amount={Amount} ClosesOrder={ClosesOrder}",
            order.Id, payment.Id, payment.GrossAmount, closesOrder);

        // Luu SAU khi gui thong bao. NotificationService chi Add() dong thong bao vao change
        // tracker — hop dong ghi ro nguoi goi phai luu — va TransactionBehavior chi Begin/Commit,
        // CommitTransactionAsync cung khong goi SaveChanges. Luu truoc roi moi Notify nghia la
        // dong thong bao duoc them vao bo nho roi bien mat, khong bao loi gi ca.
        await _uow.SaveChangesAsync(ct);

        return VnPayIpnOutcome.Confirmed;
    }

    /// <summary>
    /// Tiền đã rời tài khoản khách cho một đơn đã bị huỷ hoặc đã được trả bằng đường khác.
    ///
    /// <para>Không áp vào đơn — áp vào là thu hai lần, hoặc hồi sinh một đơn đã huỷ. Nhưng cũng không
    /// được để khoản đó biến mất: bản ghi giữ lại đúng sự thật VNPay báo (mã giao dịch, thời điểm) —
    /// mã giao dịch cũng chính là thứ lệnh hoàn qua VNPay cần — trạng thái <c>Failed</c> nghĩa là
    /// "không áp vào đơn", Admin được báo qua đúng <see cref="PaymentIncident"/> mà luồng vé dùng, và
    /// người trả tiền được báo thẳng chuyện gì đã xảy ra.</para>
    /// </summary>
    private async Task<VnPayIpnOutcome> RecordNotAppliedAsync(
        Payment payment, FnbOrder order, VnPayCallbackResult callbackResult, string? txnRef,
        bool paidElsewhere, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        payment.Status = PaymentStatus.Failed;
        payment.TransactionId = callbackResult.TransactionId;
        payment.VnPayResponseCode = callbackResult.ResponseCode;
        payment.PaidAt = now;
        payment.UpdatedAt = now;
        _uow.Repository<Payment, int>().Update(payment);

        var why = paidElsewhere
            ? "đơn F&B đã được thanh toán trước đó (khoản trả trùng)"
            : "đơn F&B đã bị huỷ";

        if (payment.PayerId is { } payerId)
        {
            await _notifications.NotifyAsync(
                payerId, NotificationType.FnbOrderUpdate,
                "Giao dịch không được ghi vào đơn",
                paidElsewhere
                    ? $"Giao dịch {payment.GrossAmount:N0}đ (mã {callbackResult.TransactionId}) cho đơn " +
                      $"#{order.Id} đã bị trừ tiền, nhưng đơn này đã được thanh toán trước đó nên đây là " +
                      "khoản trả trùng và không được ghi vào đơn. Admin đã được báo để đối soát khoản này. " +
                      "Bạn không cần thanh toán thêm — hãy giữ mã giao dịch để đối chiếu."
                    : $"Giao dịch {payment.GrossAmount:N0}đ (mã {callbackResult.TransactionId}) cho đơn " +
                      $"#{order.Id} đã bị trừ tiền, nhưng đơn này đã bị huỷ trước đó nên không được ghi " +
                      "nhận. Admin đã được báo để đối soát khoản này — hãy giữ mã giao dịch để đối chiếu.",
                referenceType: "fnbOrder", referenceId: order.Id.ToString(), ct: ct);
        }

        // Luu ban ghi thanh toan va thong bao cho khach; PaymentIncident tu luu phan cua no.
        await _uow.SaveChangesAsync(ct);

        await PaymentIncident.RecordConfirmedTooLateAsync(
            _uow, _notifications, _logger, why, txnRef, callbackResult.Amount,
            "payment", payment.Id.ToString(), ct);

        return VnPayIpnOutcome.ConfirmedTooLate;
    }
}
