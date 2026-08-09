using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Subscriptions.Commands.ProcessSubscriptionPayment;

internal sealed class ProcessSubscriptionPaymentCommandHandler
    : IRequestHandler<ProcessSubscriptionPaymentCommand, bool>
{
    private readonly IUnitOfWork _uow;
    private readonly IVnPayService _vnPay;
    private readonly ILedgerService _ledger;
    private readonly IAsyncKeyedLock _lock;
    private readonly INotificationService _notifications;
    private readonly ILogger<ProcessSubscriptionPaymentCommandHandler> _logger;

    public ProcessSubscriptionPaymentCommandHandler(
        IUnitOfWork uow, IVnPayService vnPay, ILedgerService ledger, IAsyncKeyedLock @lock,
        INotificationService notifications, ILogger<ProcessSubscriptionPaymentCommandHandler> logger)
    {
        _uow = uow;
        _vnPay = vnPay;
        _ledger = ledger;
        _lock = @lock;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessSubscriptionPaymentCommand request, CancellationToken ct)
    {
        var result = _vnPay.VerifyCallback(request.QueryParams);
        request.QueryParams.TryGetValue("vnp_TxnRef", out var txnRef);

        if (!result.IsSignatureValid)
        {
            _logger.LogWarning(
                "VNPay subscription callback rejected — invalid signature: TxnRef={TxnRef} at {At}",
                txnRef, DateTimeOffset.UtcNow);
            return false;
        }

        var paymentRepo = _uow.Repository<Payment, int>();
        var initialMatches = await paymentRepo.FindAsync(
            p => p.OrderId == txnRef && p.ReferenceType == "Subscription", ct);
        var paymentLookup = initialMatches.FirstOrDefault();
        if (paymentLookup?.PayerId is null)
        {
            _logger.LogWarning(
                "VNPay subscription callback rejected — no Payment found for TxnRef={TxnRef} at {At}",
                txnRef, DateTimeOffset.UtcNow);
            return false;
        }

        // Lock by OWNER, not by txnRef. A txnRef-keyed lock only serializes two callbacks for the
        // SAME order — it does nothing for the real race: an owner double-submits "Subscribe" (two
        // tabs, a slow redirect tapped twice), gets two DIFFERENT Payment rows with two different
        // txnRefs, and if both genuinely succeed at VNPay, their two confirmation callbacks arrive
        // with different txnRefs and never contend for the old lock at all — both would sail through
        // to "insert an Active OwnerSubscription" before either sees the other's write. Locking by
        // owner forces the second callback to wait for the first to fully commit, so the
        // already-Active check below sees accurate state.
        await using var _ = await _lock.AcquireAsync($"vnpay-subscription-owner:{paymentLookup.PayerId.Value}", ct);

        // Re-fetch inside the lock — paymentLookup may already be stale by the time the lock was
        // granted (e.g. a prior holder for this owner just confirmed a different payment).
        var payment = await paymentRepo.GetByIdAsync(paymentLookup.Id, ct);
        if (payment is null) return false;

        // Idempotency: VNPay co the goi callback nhieu lan.
        if (payment.Status != PaymentStatus.Pending)
        {
            _logger.LogInformation(
                "VNPay subscription callback replay — PaymentId={PaymentId} TxnRef={TxnRef} already {Status} at {At}",
                payment.Id, txnRef, payment.Status, DateTimeOffset.UtcNow);
            return payment.Status == PaymentStatus.Confirmed;
        }

        // Signature only proves VNPay sent this callback, not that it's for the amount we asked
        // for — fail closed on mismatch instead of confirming an unexpected amount.
        if (result.IsSuccess && result.Amount != payment.GrossAmount)
        {
            _logger.LogWarning(
                "VNPay subscription callback rejected — amount mismatch: PaymentId={PaymentId} TxnRef={TxnRef} Expected={Expected} Received={Received} at {At}",
                payment.Id, txnRef, payment.GrossAmount, result.Amount, DateTimeOffset.UtcNow);
            return false;
        }

        var now = DateTimeOffset.UtcNow;

        if (!result.IsSuccess)
        {
            payment.Status = PaymentStatus.Failed;
            payment.VnPayResponseCode = result.ResponseCode;
            payment.UpdatedAt = now;
            paymentRepo.Update(payment);
            await _uow.SaveChangesAsync(ct);
            return false;
        }

        var package = await _uow.Repository<SubscriptionPackage, int>().GetByIdAsync(
            int.Parse(payment.ReferenceId), ct);
        if (package is null || payment.PayerId is null) return false;

        var ownerId = payment.PayerId.Value;

        payment.Status = PaymentStatus.Confirmed;
        payment.TransactionId = result.TransactionId;
        payment.VnPayResponseCode = result.ResponseCode;
        payment.PaidAt = now;
        payment.UpdatedAt = now;
        paymentRepo.Update(payment);

        // Closes the actual double-charge race (see the owner-keyed lock comment above): if this
        // owner already has an Active subscription — most likely the OTHER half of a double-submit,
        // now confirmed first because it acquired the lock first — this payment is real money VNPay
        // genuinely collected for an entitlement the owner doesn't need a second copy of. Book it
        // (Confirmed, with its own ledger journal — the platform did receive this money, it's not
        // phantom revenue, just a liability for a refund) rather than leaving it at Pending forever,
        // don't create a duplicate OwnerSubscription, and tell the owner explicitly instead of
        // silently keeping a payment nobody will ever notice needs reconciling.
        var hasActiveSubscription = await _uow.Repository<OwnerSubscription, int>().AnyAsync(
            s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active, ct);
        if (hasActiveSubscription)
        {
            var dupJournalId = Guid.NewGuid().ToString("N");
            await _ledger.WriteJournalAsync(
                dupJournalId,
                LedgerReferenceTypes.Subscription,
                payment.ReferenceId,
                payment.Id,
                new LedgerLine[]
                {
                    new(AccountType.Gateway, null, payment.GrossAmount, IsDebit: true,
                        Description: $"Subscription payment #{payment.Id} (duplicate — owner already Active, needs refund)"),
                    new(AccountType.Platform, null, payment.GrossAmount, IsDebit: false,
                        Description: $"Subscription payment #{payment.Id} (duplicate — owner already Active, needs refund)")
                }, ct);

            await _uow.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(
                ownerId,
                NotificationType.DuplicatePaymentDetected,
                "Phát hiện thanh toán trùng",
                $"Bạn vừa thanh toán {payment.GrossAmount:N0}đ cho gói subscription trong khi đã có gói đang hoạt động. " +
                "Khoản này sẽ được xem xét hoàn lại — vui lòng liên hệ hỗ trợ nếu không thấy phản hồi trong vài ngày làm việc.",
                referenceType: "payment", referenceId: payment.Id.ToString(), ct: ct);
            await _uow.SaveChangesAsync(ct);

            return true;
        }

        var expiresAt = package.BillingCycle switch
        {
            SubscriptionBillingCycle.Monthly => now.AddMonths(1),
            SubscriptionBillingCycle.Quarterly => now.AddMonths(3),
            SubscriptionBillingCycle.Yearly => now.AddYears(1),
            _ => now.AddMonths(1)
        };

        var subscription = new OwnerSubscription
        {
            OwnerId = payment.PayerId.Value,
            PackageId = package.Id,
            StartedAt = now,
            ExpiresAt = expiresAt,
            Status = SubscriptionStatus.Active,
            AutoRenew = false,
            // From the Payment snapshot taken at checkout, NOT the freshly-refetched package above
            // (only used for BillingCycle/Price-verification) — package.MaxTicketsPerEvent/HasAiPoster
            // could have been edited by an admin in the window between checkout and this callback.
            MaxTicketsPerEventSnapshot = payment.SubscriptionMaxTicketsPerEventSnapshot ?? package.MaxTicketsPerEvent,
            HasAiPosterSnapshot = payment.SubscriptionHasAiPosterSnapshot ?? package.HasAiPoster
        };
        _uow.Repository<OwnerSubscription, int>().Add(subscription);

        // Subscription la doanh thu 100% cua platform (khong chia se voi owner/khong tru thue
        // ho ben thu 3 nhu ve/donate) - khac voi J1 ticket journal co 3 ben.
        //
        // Book payment.GrossAmount (the snapshot taken at checkout, already verified above against
        // VNPay's callback amount) — NOT package.Price. package is re-fetched fresh here, so if an
        // admin edits SubscriptionPackage.Price while this payment is in flight (between checkout
        // and VNPay confirming), package.Price would silently diverge from what VNPay actually
        // collected, permanently misstating booked revenue for this transaction.
        var journalId = Guid.NewGuid().ToString("N");
        await _ledger.WriteJournalAsync(
            journalId,
            LedgerReferenceTypes.Subscription,
            payment.ReferenceId,
            payment.Id,
            new LedgerLine[]
            {
                new(AccountType.Gateway, null, payment.GrossAmount, IsDebit: true,
                    Description: $"Subscription payment #{payment.Id}"),
                new(AccountType.Platform, null, payment.GrossAmount, IsDebit: false,
                    Description: $"Subscription payment #{payment.Id}")
            }, ct);

        await _uow.SaveChangesAsync(ct);
        return true;
    }
}
