using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbOrders.Commands.ProcessFnbOrderPayment;

internal sealed class ProcessFnbOrderPaymentCommandHandler
    : IRequestHandler<ProcessFnbOrderPaymentCommand, bool>
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

    public async Task<bool> Handle(ProcessFnbOrderPaymentCommand request, CancellationToken ct)
    {
        var callbackResult = _vnPay.VerifyCallback(request.QueryParams);

        if (!callbackResult.IsSignatureValid)
        {
            request.QueryParams.TryGetValue("vnp_TxnRef", out var rejectedTxnRef);
            _logger.LogWarning(
                "VNPay F&B callback rejected: invalid signature. TxnRef={TxnRef}", rejectedTxnRef);
            return false;
        }

        request.QueryParams.TryGetValue("vnp_TxnRef", out var txnRef);

        // Same reasoning as ProcessDonationPaymentCommandHandler/ProcessVnPayCallbackCommandHandler:
        // VNPay retries the IPN callback for up to ~15 minutes if it doesn't get back the exact
        // response it expects — without this lock, 2 near-simultaneous callbacks could both mark
        // the order Paid and both write a ledger journal for the same payment.
        await using var _ = await _lock.AcquireAsync($"vnpay-fnborder:{txnRef}", ct);

        var payments = await _uow.Repository<Payment, int>().FindAsync(p => p.OrderId == txnRef, ct);
        var payment = payments.FirstOrDefault();
        if (payment is null) return false;

        // Idempotency: only process if still in initial state.
        if (payment.Status != PaymentStatus.Pending)
            return payment.Status == PaymentStatus.Confirmed;

        if (callbackResult.IsSuccess && callbackResult.Amount != payment.GrossAmount)
        {
            _logger.LogWarning(
                "VNPay F&B callback amount mismatch: PaymentId={PaymentId} Expected={Expected} Actual={Actual}",
                payment.Id, payment.GrossAmount, callbackResult.Amount);
            return false;
        }

        if (!callbackResult.IsSuccess)
        {
            payment.Status = PaymentStatus.Failed;
            _uow.Repository<Payment, int>().Update(payment);
            await _uow.SaveChangesAsync(ct);
            _logger.LogWarning(
                "VNPay F&B payment failed: PaymentId={PaymentId} ResponseCode={ResponseCode}",
                payment.Id, callbackResult.ResponseCode);
            return false;
        }

        var order = await _uow.Repository<FnbOrder, int>()
            .GetByIdAsync(int.Parse(payment.ReferenceId), ct);
        if (order is null) return false;

        // F&B is commission-free (same premise as UpdateFnbOrderStatusCommandHandler's cash Paid
        // path) — net equals gross, no platform/tax split.
        payment.NetAmount = payment.GrossAmount;
        payment.Status = PaymentStatus.Confirmed;
        payment.TransactionId = callbackResult.TransactionId;
        payment.VnPayResponseCode = callbackResult.ResponseCode;
        payment.PaidAt = DateTimeOffset.UtcNow;
        _uow.Repository<Payment, int>().Update(payment);

        order.Status = FnbOrderStatus.Paid;
        order.UpdatedAt = DateTimeOffset.UtcNow;
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
                $"Đơn #{order.Id} của bạn đã thanh toán thành công {payment.GrossAmount:N0}đ.",
                referenceType: "fnbOrder", referenceId: order.Id.ToString(), ct: ct);
        }

        _logger.LogInformation(
            "F&B order payment confirmed: OrderId={OrderId} PaymentId={PaymentId} Amount={Amount}",
            order.Id, payment.Id, payment.GrossAmount);

        return true;
    }
}
