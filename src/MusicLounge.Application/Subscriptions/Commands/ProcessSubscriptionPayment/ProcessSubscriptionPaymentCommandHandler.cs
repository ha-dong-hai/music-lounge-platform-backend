using MediatR;
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

    public ProcessSubscriptionPaymentCommandHandler(
        IUnitOfWork uow, IVnPayService vnPay, ILedgerService ledger, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _vnPay = vnPay;
        _ledger = ledger;
        _lock = @lock;
    }

    public async Task<bool> Handle(ProcessSubscriptionPaymentCommand request, CancellationToken ct)
    {
        var result = _vnPay.VerifyCallback(request.QueryParams);
        if (!result.IsSignatureValid) return false;

        request.QueryParams.TryGetValue("vnp_TxnRef", out var txnRef);

        // Same VNPay retry-storm hazard as donations — without this lock, 2 near-simultaneous
        // callbacks for the same order both read Status==Pending and both create an
        // OwnerSubscription + ledger journal (double-charge, 2 active subscriptions).
        await using var _ = await _lock.AcquireAsync($"vnpay-subscription:{txnRef}", ct);

        var paymentRepo = _uow.Repository<Payment, int>();
        var payments = await paymentRepo.FindAsync(
            p => p.OrderId == txnRef && p.ReferenceType == "Subscription", ct);
        var payment = payments.FirstOrDefault();
        if (payment is null) return false;

        // Idempotency: VNPay co the goi callback nhieu lan.
        if (payment.Status != PaymentStatus.Pending)
            return payment.Status == PaymentStatus.Confirmed;

        // Signature only proves VNPay sent this callback, not that it's for the amount we asked
        // for — fail closed on mismatch instead of confirming an unexpected amount.
        if (result.IsSuccess && result.Amount != payment.GrossAmount)
            return false;

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

        payment.Status = PaymentStatus.Confirmed;
        payment.TransactionId = result.TransactionId;
        payment.VnPayResponseCode = result.ResponseCode;
        payment.PaidAt = now;
        payment.UpdatedAt = now;
        paymentRepo.Update(payment);

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
