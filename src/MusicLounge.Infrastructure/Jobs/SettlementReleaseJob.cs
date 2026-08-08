using Microsoft.EntityFrameworkCore;
using Hangfire;
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

    public SettlementReleaseJob(
        ApplicationDbContext ctx, ILedgerService ledger, ISystemConfigService config,
        INotificationService notifications)
    {
        _ctx = ctx;
        _ledger = ledger;
        _config = config;
        _notifications = notifications;
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
            "settlement_completion_threshold_pct", 0.70m, ct);

        foreach (var settlement in due)
        {
            if (settlement.ReleaseType == SettlementReleaseType.Final30)
            {
                var completionOk = await IsShowCompletionAcceptableAsync(settlement.PaymentId, threshold, ct);
                if (!completionOk)
                {
                    settlement.Status = SettlementStatus.PendingReview;
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

            await _notifications.NotifyAsync(
                settlement.OwnerId,
                NotificationType.SettlementReleased,
                "Đã nhận thanh toán",
                $"Khoản thanh toán {settlement.NetAmount:N0}đ ({settlement.ReleaseType}) đã được giải ngân.",
                referenceType: "settlement",
                referenceId: settlement.Id.ToString(),
                ct: ct);
        }

        await _ctx.SaveChangesAsync(ct);
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

        var scheduledEnd = show.ScheduledEnd ?? show.ScheduledStart.AddHours(4);
        var scheduledDuration = scheduledEnd - show.ScheduledStart;
        var actualDuration = show.ActualEnd.Value - show.ActualStart.Value;

        if (scheduledDuration <= TimeSpan.Zero) return true;

        var ratio = (decimal)(actualDuration / scheduledDuration);
        return ratio >= threshold;
    }
}
