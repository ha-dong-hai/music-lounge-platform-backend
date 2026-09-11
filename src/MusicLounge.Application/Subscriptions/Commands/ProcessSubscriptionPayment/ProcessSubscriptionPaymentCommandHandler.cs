using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Subscriptions.Commands.ProcessSubscriptionPayment;

internal sealed class ProcessSubscriptionPaymentCommandHandler
    : IRequestHandler<ProcessSubscriptionPaymentCommand, VnPayIpnOutcome>
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

    public async Task<VnPayIpnOutcome> Handle(ProcessSubscriptionPaymentCommand request, CancellationToken ct)
    {
        var result = _vnPay.VerifyCallback(request.QueryParams);
        request.QueryParams.TryGetValue("vnp_TxnRef", out var txnRef);

        if (!result.IsSignatureValid)
        {
            _logger.LogWarning(
                "VNPay subscription callback rejected — invalid signature: TxnRef={TxnRef} at {At}",
                txnRef, DateTimeOffset.UtcNow);
            return VnPayIpnOutcome.InvalidSignature;
        }

        var paymentRepo = _uow.Repository<Payment, int>();
        var initialMatches = await paymentRepo.FindAsync(
            p => p.OrderId == txnRef && p.ReferenceType == SubscriptionPayments.ReferenceType, ct);
        var paymentLookup = initialMatches.FirstOrDefault();
        if (paymentLookup?.PayerId is null)
        {
            _logger.LogWarning(
                "VNPay subscription callback rejected — no Payment found for TxnRef={TxnRef} at {At}",
                txnRef, DateTimeOffset.UtcNow);
            return VnPayIpnOutcome.OrderNotFound;
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
        if (payment is null) return VnPayIpnOutcome.OrderNotFound;

        // Idempotency: VNPay co the goi callback nhieu lan.
        if (payment.Status != PaymentStatus.Pending)
        {
            // MLACP-334. Xem chu thich cung ten o duong ve.
            if (result.IsSuccess && payment.Status != PaymentStatus.Confirmed)
            {
                await PaymentIncident.RecordConfirmedTooLateAsync(
                    _uow, _notifications, _logger, "goi dang ky", txnRef, result.Amount,
                    "payment", payment.Id.ToString(), ct);
                return VnPayIpnOutcome.ConfirmedTooLate;
            }

            _logger.LogInformation(
                "VNPay subscription callback replay — PaymentId={PaymentId} TxnRef={TxnRef} already {Status} at {At}",
                payment.Id, txnRef, payment.Status, DateTimeOffset.UtcNow);
            return VnPayIpnOutcome.AlreadyProcessed;
        }

        // Signature only proves VNPay sent this callback, not that it's for the amount we asked
        // for — fail closed on mismatch instead of confirming an unexpected amount.
        if (result.IsSuccess && result.Amount != payment.GrossAmount)
        {
            _logger.LogWarning(
                "VNPay subscription callback rejected — amount mismatch: PaymentId={PaymentId} TxnRef={TxnRef} Expected={Expected} Received={Received} at {At}",
                payment.Id, txnRef, payment.GrossAmount, result.Amount, DateTimeOffset.UtcNow);
            return VnPayIpnOutcome.AmountMismatch;
        }

        var now = DateTimeOffset.UtcNow;

        if (!result.IsSuccess)
        {
            payment.Status = PaymentStatus.Failed;
            payment.VnPayResponseCode = result.ResponseCode;
            payment.UpdatedAt = now;
            paymentRepo.Update(payment);
            await _uow.SaveChangesAsync(ct);
            return VnPayIpnOutcome.RecordedAsFailed;
        }

        var package = await _uow.Repository<SubscriptionPackage, int>().GetByIdAsync(
            int.Parse(payment.ReferenceId), ct);
        if (package is null || payment.PayerId is null) return VnPayIpnOutcome.InternalError;

        var ownerId = payment.PayerId.Value;

        payment.Status = PaymentStatus.Confirmed;
        payment.TransactionId = result.TransactionId;
        payment.VnPayResponseCode = result.ResponseCode;
        payment.PaidAt = now;
        payment.UpdatedAt = now;
        paymentRepo.Update(payment);

        // MLACP-371: gia han som va doi goi. Mot thanh toan goi den trong luc chu dang co goi Active khong phai luc nao
        // cung la thanh toan trung: thanh toan tao tu lenh Gia han hoac Doi goi la dung thu chu muon mua. Chi thanh toan
        // tu lenh Dang ky moi di duong "thanh toan trung" ben duoi.
        var purchase = SubscriptionTerms.PurchaseOf(payment.OrderId);
        var currentPlan = (await _uow.Repository<OwnerSubscription, int>().FindAsync(
                s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active, ct))
            .FirstOrDefault();
        if (currentPlan is not null && purchase != SubscriptionPurchase.Subscribe)
        {
            string summary;
            if (purchase == SubscriptionPurchase.Renew && currentPlan.PackageId == package.Id)
            {
                summary = ExtendPlan(currentPlan, package, payment, now);
                // FindAsync doc AsNoTracking — khong Update thi han moi khong duoc luu du VNPay da thu tien.
                _uow.Repository<OwnerSubscription, int>().Update(currentPlan);
            }
            else
            {
                summary = await ChangePlanAsync(currentPlan, package, payment, ownerId, now, ct);
            }

            await _ledger.WriteJournalAsync(
                Guid.NewGuid().ToString("N"),
                LedgerReferenceTypes.Subscription,
                payment.ReferenceId,
                payment.Id,
                new LedgerLine[]
                {
                    new(AccountType.Gateway, null, payment.GrossAmount, IsDebit: true,
                        Description: $"Subscription payment #{payment.Id} ({purchase})"),
                    new(AccountType.Platform, null, payment.GrossAmount, IsDebit: false,
                        Description: $"Subscription payment #{payment.Id} ({purchase})")
                }, ct);

            await _notifications.NotifyAsync(
                ownerId,
                NotificationType.SubscriptionUpdated,
                purchase == SubscriptionPurchase.Renew ? "Gói dịch vụ đã được gia hạn" : "Đã đổi gói dịch vụ",
                summary,
                referenceType: "payment",
                referenceId: payment.Id.ToString(),
                ct: ct);

            await _uow.SaveChangesAsync(ct);
            return VnPayIpnOutcome.Confirmed;
        }

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

            // The notification below promises the owner this will be refunded, and the journal above
            // books it as a liability — but nothing used to turn either into work anybody could
            // actually pick up. The money sat in the platform's account labelled "needs refund",
            // absent from the Admin refund queue, with the owner told to chase support if they heard
            // nothing. Raise the real request so it enters the same queue, SLA and overdue alerting
            // as every other refund (RefundSlaBreachAlertJob), instead of depending on someone
            // noticing a ledger description.
            _uow.Repository<RefundRequest, int>().Add(new RefundRequest
            {
                PaymentId = payment.Id,
                RequestedBy = ownerId,
                Reason = "Thanh toán trùng gói subscription — owner đã có gói đang hoạt động",
                AmountRequested = payment.GrossAmount,
                RefundPercentage = 100m,
                Status = RefundRequestStatus.Pending
            });

            await _uow.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(
                ownerId,
                NotificationType.DuplicatePaymentDetected,
                "Phát hiện thanh toán trùng",
                $"Bạn vừa thanh toán {payment.GrossAmount:N0}đ cho gói subscription trong khi đã có gói đang hoạt động. " +
                "Khoản này sẽ được xem xét hoàn lại — yêu cầu hoàn tiền đã được tạo tự động và sẽ được xử lý theo đúng thời hạn cam kết.",
                referenceType: "payment", referenceId: payment.Id.ToString(), ct: ct);
            await _uow.SaveChangesAsync(ct);

            return VnPayIpnOutcome.Confirmed;
        }

        var expiresAt = SubscriptionTerms.CycleEnd(package.BillingCycle, now);

        var subscription = new OwnerSubscription
        {
            OwnerId = payment.PayerId.Value,
            PackageId = package.Id,
            StartedAt = now,
            ExpiresAt = expiresAt,
            Status = SubscriptionStatus.Active,
            AmountPaid = payment.GrossAmount,
            // From the Payment snapshot taken at checkout, NOT the freshly-refetched package above
            // (only used for BillingCycle/Price-verification) — package.MaxTicketsPerEvent/HasAiPoster
            // could have been edited by an admin in the window between checkout and this callback.
            MaxTicketsPerEventSnapshot = payment.SubscriptionMaxTicketsPerEventSnapshot ?? package.MaxTicketsPerEvent,
            HasAiPosterSnapshot = payment.SubscriptionHasAiPosterSnapshot ?? package.HasAiPoster,
            MaxAiPostersPerMonthSnapshot = payment.SubscriptionMaxAiPostersPerMonthSnapshot ?? package.MaxAiPostersPerMonth,
            MaxTourScenesSnapshot = payment.SubscriptionMaxTourScenesSnapshot ?? package.MaxTourScenes
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
        return VnPayIpnOutcome.Confirmed;
    }

    /// <summary>MLACP-371: gia hạn — cộng một kỳ nối vào hạn hiện tại (không mất ngày nào còn lại), bỏ trạng thái đã huỷ.</summary>
    private static string ExtendPlan(OwnerSubscription plan, SubscriptionPackage package, Payment payment, DateTimeOffset now)
    {
        plan.ExpiresAt = SubscriptionTerms.CycleEnd(package.BillingCycle, plan.ExpiresAt > now ? plan.ExpiresAt : now);
        plan.AmountPaid = (plan.AmountPaid ?? package.Price) + payment.GrossAmount;
        plan.CancelledAt = null;
        return $"Gói \"{package.Name}\" đã được gia hạn, dùng tới {VietnamTime.Format(plan.ExpiresAt, "dd/MM/yyyy")}.";
    }

    /// <summary>
    /// MLACP-371: đổi gói — gói mới có hiệu lực ngay; phần giá trị còn lại của gói cũ được quy thành thời gian ở gói
    /// mới theo giá gói mới (không hoàn tiền mặt).
    /// </summary>
    private async Task<string> ChangePlanAsync(
        OwnerSubscription current, SubscriptionPackage newPackage, Payment payment, int ownerId,
        DateTimeOffset now, CancellationToken ct)
    {
        var oldPackage = await _uow.Repository<SubscriptionPackage, int>().GetByIdAsync(current.PackageId, ct);
        var credit = SubscriptionTerms.RemainingValue(current, oldPackage?.Price ?? 0m, now);
        var cycleEnd = SubscriptionTerms.CycleEnd(newPackage.BillingCycle, now);
        var extra = SubscriptionTerms.TimeWorth(credit, payment.GrossAmount, cycleEnd - now);

        // Goi cu phai roi trang thai Active TRUOC khi goi moi duoc them: chi muc duy nhat (moi chu mot goi Active)
        // khong quan tam thu tu cac lenh EF gui xuong trong cung mot lan luu.
        current.Status = SubscriptionStatus.Cancelled;
        current.CancelledAt = now;
        _uow.Repository<OwnerSubscription, int>().Update(current);
        await _uow.SaveChangesAsync(ct);

        var plan = new OwnerSubscription
        {
            OwnerId = ownerId,
            PackageId = newPackage.Id,
            StartedAt = now,
            ExpiresAt = cycleEnd + extra,
            Status = SubscriptionStatus.Active,
            AmountPaid = payment.GrossAmount + credit,
            MaxTicketsPerEventSnapshot = payment.SubscriptionMaxTicketsPerEventSnapshot ?? newPackage.MaxTicketsPerEvent,
            HasAiPosterSnapshot = payment.SubscriptionHasAiPosterSnapshot ?? newPackage.HasAiPoster,
            MaxAiPostersPerMonthSnapshot = payment.SubscriptionMaxAiPostersPerMonthSnapshot ?? newPackage.MaxAiPostersPerMonth,
            MaxTourScenesSnapshot = payment.SubscriptionMaxTourScenesSnapshot ?? newPackage.MaxTourScenes
        };
        _uow.Repository<OwnerSubscription, int>().Add(plan);

        return $"Đã chuyển sang gói \"{newPackage.Name}\". Phần còn lại của gói cũ " +
               $"({SubscriptionTerms.DescribeCredit(credit, extra)}) đã được cộng vào — gói mới dùng tới " +
               $"{VietnamTime.Format(plan.ExpiresAt, "dd/MM/yyyy")}.";
    }
}
