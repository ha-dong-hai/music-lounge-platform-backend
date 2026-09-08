using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hangfire;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// W27 — releases Settlement rows whose ScheduledAt has passed: writes the payout ledger
/// journal and marks the row Released. D16: a Final30 tranche is only auto-released if the
/// show's actual/scheduled duration ratio meets the configured completion threshold —
/// otherwise it's parked as PendingReview for Admin to decide.
/// </summary>
public sealed class SettlementReleaseJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly ILedgerService _ledger;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;
    private readonly ILogger<SettlementReleaseJob> _logger;

    public SettlementReleaseJob(
        ApplicationDbContext ctx, ILedgerService ledger, ISystemConfigService config,
        INotificationService notifications, ILogger<SettlementReleaseJob> logger)
    {
        _ctx = ctx;
        _ledger = ledger;
        _config = config;
        _notifications = notifications;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        // Filter by Status server-side, then filter ScheduledAt client-side — combining an
        // enum equality with a DateTimeOffset comparison in one query fails to translate
        // under the SQLite provider used in tests.
        var scheduled = await _ctx.Settlements
            .Where(s => s.Status == SettlementStatus.Scheduled)
            .ToListAsync(ct);
        var due = scheduled.Where(s => s.ScheduledAt <= now).ToList();

        if (due.Count == 0) return;

        var threshold = await _config.GetDecimalAsync(
            ConfigKeys.SettlementCompletionThresholdPct, 0.70m, ct);

        // A payment whose refund is still awaiting an Admin decision must not be paid out to the
        // owner yet. ProcessRefundRequest is the ONLY thing that shrinks a scheduled settlement to
        // match a refund, and it is a manual Admin action with no auto-processing job behind it —
        // while CancelLoungeShow (and ResolveComplaint / ResolveContentReport, which also cancel a
        // show) create the RefundRequest as Pending and leave the settlements untouched. So a show
        // cancelled with tickets sold left both tranches Scheduled: at showEnd+48h and showEnd+14d
        // this job paid the owner in full for tickets the buyers were about to be refunded 100% of,
        // and the platform covered both sides. The ledger is append-only, so undoing that needs a
        // manual reversing journal.
        //
        // Deferring rather than cancelling is what makes this safe in BOTH directions: if the
        // refund is approved, ProcessRefundRequest shrinks the tranche and the next daily run pays
        // the correct reduced amount; if it is rejected, nothing is pending any more and the next
        // run pays in full. Neither outcome is pre-judged here, and no money is stranded.
        var duePaymentIds = due.Select(s => s.PaymentId).Distinct().ToList();
        var paymentsAwaitingRefundDecision = (await _ctx.RefundRequests
                .Where(r => duePaymentIds.Contains(r.PaymentId) && r.Status == RefundRequestStatus.Pending)
                .Select(r => r.PaymentId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var settlement in due)
        {
            if (paymentsAwaitingRefundDecision.Contains(settlement.PaymentId))
            {
                _logger.LogWarning(
                    "Settlement release deferred — PaymentId={PaymentId} SettlementId={SettlementId} " +
                    "has a refund request still pending an Admin decision at {At}",
                    settlement.PaymentId, settlement.Id, now);
                continue;
            }

            // Same defer-don't-pre-judge rule, for a tranche with nowhere to pay into.
            // ScheduleSettlementHandler no longer refuses to create a settlement when the venue has
            // no default BankAccount — refusing there destroyed the buyer's already-paid purchase —
            // so it records the debt with a null destination instead. Writing the payout journal now
            // would credit the owner's ledger account for money no bank transfer can follow, and the
            // ledger is append-only. Hold it until an account exists; the next daily run pays it.
            if (settlement.BankAccountId is null)
            {
                _logger.LogError(
                    "Settlement release deferred — SettlementId={SettlementId} OwnerId={OwnerId} has no " +
                    "payout account. The venue must register a default BankAccount before this can be " +
                    "released at {At}",
                    settlement.Id, settlement.OwnerId, now);
                continue;
            }

            if (settlement.ReleaseType == SettlementReleaseType.Final30)
            {
                var completionOk = await IsShowCompletionAcceptableAsync(settlement.PaymentId, threshold, ct);
                if (!completionOk)
                {
                    settlement.Status = SettlementStatus.PendingReview;
                    await _ctx.SaveChangesAsync(ct);
                    continue;
                }
            }

            var journalId = Guid.NewGuid().ToString("N");

            await _ledger.WriteJournalAsync(
                journalId,
                LedgerReferenceTypes.Settlement,
                settlement.Id.ToString(),
                settlement.PaymentId,
                new LedgerLine[]
                {
                    new(AccountType.Platform, null, settlement.NetAmount, IsDebit: true,
                        Description: $"Settlement #{settlement.Id} payout"),
                    new(AccountType.User, settlement.OwnerId, settlement.NetAmount, IsDebit: false,
                        Description: $"Settlement #{settlement.Id} payout")
                }, ct);

            settlement.Status = SettlementStatus.Released;
            settlement.ReleasedAt = now;
            settlement.LedgerJournalId = journalId;

            // Commit THIS settlement's own release before enqueuing its notification, not once at
            // the end of the whole batch — NotifyAsync's FCM push enqueues directly and durably to
            // Hangfire's own storage (IBackgroundJobService), independent of this DbContext's
            // transaction. With a single trailing SaveChangesAsync, a later settlement in the same
            // run throwing would leave this one's Release un-persisted while its push notification
            // had already fired; Hangfire's automatic retry would then re-process this
            // still-"Scheduled" settlement from scratch, sending a second "payment released"
            // notification for it. Saving per-item makes each settlement's release+notify atomic
            // and the loop safely resumable.
            await _ctx.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(
                settlement.OwnerId,
                NotificationType.SettlementReleased,
                "Đã nhận thanh toán",
                $"Khoản thanh toán {settlement.NetAmount:N0}đ ({settlement.ReleaseType}) đã được giải ngân.",
                referenceType: "settlement",
                referenceId: settlement.Id.ToString(),
                ct: ct);

            // NotifyAsync only staged a Notification row (Add()) — flush that too before moving on,
            // so it isn't silently rolled into whatever the NEXT settlement's SaveChangesAsync
            // happens to touch (harmless today since Notification is its own row, but keeps this
            // settlement's unit of work fully self-contained).
            await _ctx.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// D16: actual_duration / scheduled_duration >= threshold. If the show was never marked
    /// started/ended (no tracking mechanism wired up yet), we can't judge completion — assume
    /// normal completion rather than blocking every final payout forever.
    /// </summary>
    private async Task<bool> IsShowCompletionAcceptableAsync(
        int paymentId, decimal threshold, CancellationToken ct)
    {
        var showId = await _ctx.Tickets
            .Where(t => t.PaymentId == paymentId)
            .Select(t => (int?)t.ShowId)
            .FirstOrDefaultAsync(ct);
        if (showId is null) return true;

        var show = await _ctx.LoungeShows.FirstOrDefaultAsync(s => s.Id == showId, ct);
        if (show is null || show.ActualStart is null || show.ActualEnd is null)
            return true;

        var scheduledEnd = ShowSchedule.EffectiveEnd(show);
        var scheduledDuration = scheduledEnd - show.ScheduledStart;
        var actualDuration = show.ActualEnd.Value - show.ActualStart.Value;

        if (scheduledDuration <= TimeSpan.Zero) return true;

        var ratio = (decimal)(actualDuration / scheduledDuration);
        return ratio >= threshold;
    }
}
