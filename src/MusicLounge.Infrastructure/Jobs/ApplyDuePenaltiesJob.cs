using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// §6.8 — applies the delayed venue-status change and subscription compensation for
/// Suspension/Ban penalties once EffectiveAt arrives. Warning has no delay and is applied
/// immediately by IssuePenaltyCommandHandler, so it never appears here. Idempotent by construction:
/// re-running against an already-applied penalty is a no-op because the venue's Status already
/// matches the target (checked before doing anything), so a penalty is never compensated twice
/// even if the job overlaps itself or retries.
/// </summary>
public sealed class ApplyDuePenaltiesJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly ILedgerService _ledger;
    private readonly INotificationService _notifications;

    public ApplyDuePenaltiesJob(
        ApplicationDbContext ctx, ILedgerService ledger, INotificationService notifications)
    {
        _ctx = ctx;
        _ledger = ledger;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        // Filter by Status server-side, then filter EffectiveAt client-side — same pattern as
        // SettlementReleaseJob/ReleaseExpiredHoldsJob: combining an enum-equality predicate with a
        // DateTimeOffset comparison in one query does not translate under the SQLite test provider.
        var active = await _ctx.VenuePenalties
            .Where(p => p.Status == PenaltyStatus.Active
                && (p.PenaltyType == PenaltyType.Suspension || p.PenaltyType == PenaltyType.Ban))
            .ToListAsync(ct);
        var due = active.Where(p => p.EffectiveAt <= now).ToList();
        if (due.Count == 0) return;

        foreach (var penalty in due)
        {
            var lounge = await _ctx.Lounges.FirstOrDefaultAsync(l => l.Id == penalty.LoungeId, ct);
            if (lounge is null) continue;

            var targetStatus = penalty.PenaltyType == PenaltyType.Suspension
                ? LoungeStatus.Suspended
                : LoungeStatus.Locked;

            // Idempotency guard — if the venue is already in the target state, this penalty (or a
            // more severe one reaching the same state) has already been applied.
            if (lounge.Status == targetStatus) continue;

            lounge.Status = targetStatus;

            // Ordering by a DateTimeOffset column does not translate under the SQLite provider
            // used in tests (same limitation noted elsewhere in this codebase) — an owner should
            // only ever have one Active subscription at a time anyway, so fetch and pick client-side.
            var ownerSubscriptions = await _ctx.OwnerSubscriptions
                .Where(s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active)
                .ToListAsync(ct);
            var subscription = ownerSubscriptions.OrderByDescending(s => s.ExpiresAt).FirstOrDefault();

            if (subscription is not null)
            {
                if (penalty.PenaltyType == PenaltyType.Suspension && penalty.SuspensionDays is int days)
                {
                    // §6.8 — compensate the Owner for the outage by pushing their subscription
                    // expiry back, so a suspension doesn't also cost them paid-for platform time.
                    subscription.ExpiresAt = subscription.ExpiresAt.AddDays(days);
                }
                else if (penalty.PenaltyType == PenaltyType.Ban)
                {
                    await RefundRemainingSubscriptionAsync(subscription, penalty, ct);
                }
            }

            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.PenaltyIssued,
                penalty.PenaltyType == PenaltyType.Suspension ? "Phòng trà đã bị tạm khoá" : "Phòng trà đã bị khoá vĩnh viễn",
                $"\"{lounge.Name}\" hiện đã ở trạng thái {targetStatus} theo phạt #{penalty.Id}.",
                referenceType: "venue_penalty",
                referenceId: penalty.Id.ToString(),
                ct: ct);
        }

        await _ctx.SaveChangesAsync(ct);
    }

    private async Task RefundRemainingSubscriptionAsync(
        OwnerSubscription subscription, VenuePenalty penalty, CancellationToken ct)
    {
        var package = await _ctx.SubscriptionPackages.FirstOrDefaultAsync(p => p.Id == subscription.PackageId, ct);
        if (package is null) return;

        var totalSpan = subscription.ExpiresAt - subscription.StartedAt;
        if (totalSpan <= TimeSpan.Zero) return;

        var remaining = subscription.ExpiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return; // already expired — nothing left to refund

        var ratio = Math.Clamp((decimal)(remaining / totalSpan), 0m, 1m);
        var refundAmount = Math.Round(package.Price * ratio, 2);
        if (refundAmount <= 0m) return;

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = DateTimeOffset.UtcNow;

        // Mirrors ProcessSubscriptionPaymentCommandHandler's original journal (Gateway debit /
        // Platform credit) in reverse — subscription revenue is 100% platform's, no owner/tax
        // split, so the reversal only ever touches these two accounts.
        await _ledger.WriteJournalAsync(
            Guid.NewGuid().ToString("N"),
            LedgerReferenceTypes.Subscription,
            subscription.Id.ToString(),
            paymentId: null,
            new LedgerLine[]
            {
                new(AccountType.Platform, null, refundAmount, IsDebit: true,
                    Description: $"Hoàn tiền pro-rata subscription — phạt Ban #{penalty.Id}"),
                new(AccountType.Gateway, null, refundAmount, IsDebit: false,
                    Description: $"Hoàn tiền pro-rata subscription — phạt Ban #{penalty.Id}")
            }, ct);
    }
}
