using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// §6.17 — Appeal SLA (system_config: appeal_sla_hours) sets each penalty's AppealDeadline at
/// submission time (see SubmitAppealCommandHandler); if Admin hasn't resolved it by then, auto-approve
/// (Overturned) so an unattended appeal never leaves an Owner penalized indefinitely.
/// </summary>
public sealed class AutoApproveOverdueAppealsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;
    private readonly IAsyncKeyedLock _lock;

    public AutoApproveOverdueAppealsJob(
        ApplicationDbContext ctx, INotificationService notifications, IAsyncKeyedLock @lock)
    {
        _ctx = ctx;
        _notifications = notifications;
        _lock = @lock;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        var appealed = await _ctx.VenuePenalties
            .Where(p => p.Status == PenaltyStatus.Appealed && p.AppealDeadline != null)
            .ToListAsync(ct);
        var overdue = appealed.Where(p => p.AppealDeadline <= now).ToList();
        if (overdue.Count == 0) return;

        foreach (var penalty in overdue)
        {
            // Same lock key ReviewAppealCommandHandler uses — an Admin manually deciding right at
            // the SLA boundary can otherwise race this job. Held until this penalty's own
            // SaveChangesAsync below, not released early, so the lock actually covers the commit.
            await using var _ = await _lock.AcquireAsync($"appeal-review:{penalty.Id}", ct);

            // Re-check under the lock: a manual review that won it may have already resolved this
            // penalty (moved it off Appealed) between the query above and acquiring the lock.
            var current = await _ctx.VenuePenalties.FirstOrDefaultAsync(p => p.Id == penalty.Id, ct);
            if (current is null || current.Status != PenaltyStatus.Appealed) continue;

            current.Status = PenaltyStatus.Overturned;
            current.AppealResult = "Overturned (auto — quá hạn SLA kháng cáo)";
            current.ReviewedAt = now;

            var lounge = await _ctx.Lounges.FirstOrDefaultAsync(l => l.Id == current.LoungeId, ct);
            if (lounge is null) continue;

            var wasAlreadyApplied = current.AppliedAt is not null;

            // MLACP-367: cung quy tac voi ReviewAppeal va ExpireServedSuspensions (PenaltyLifecycle) — trang
            // thai suy tu cac an CON hieu luc, va thong bao noi dung trang thai sau cung.
            var remaining = await _ctx.VenuePenalties
                .Where(p => p.LoungeId == current.LoungeId
                            && p.Id != current.Id
                            && PenaltyLifecycle.InForce.Contains(p.Status))
                .ToListAsync(ct);
            if (PenaltyLifecycle.StatusAfterReleasing(lounge.Status, current.PenaltyType, remaining) is { } releasedStatus)
                lounge.Status = releasedStatus;

            // MLACP-369: cung cach voi ReviewAppeal — khoa vinh vien da ap roi bi huy thi tra lai goi.
            OwnerSubscription? restoredPlan = null;
            if (wasAlreadyApplied && current.PenaltyType == PenaltyType.Ban)
                restoredPlan = PenaltySubscriptions.RestoreAfterBanLifted(
                    await _ctx.OwnerSubscriptions.Where(s => s.OwnerId == lounge.OwnerId).ToListAsync(ct),
                    current, now);

            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.AppealResolved,
                "Kháng cáo tự động được chấp thuận",
                $"Admin không xử lý kháng cáo cho phạt #{current.Id} trong thời hạn SLA — kháng cáo được " +
                $"tự động chấp thuận. {PenaltyLifecycle.DescribeForOwner(lounge.Status)}".TrimEnd() +
                (restoredPlan is null ? "" : $" Gói dịch vụ đã được kích hoạt lại, hết hạn {VietnamTime.Format(restoredPlan.ExpiresAt, "dd/MM/yyyy")}."),
                referenceType: "venue_penalty",
                referenceId: current.Id.ToString(),
                ct: ct);

            await _ctx.SaveChangesAsync(ct);
        }
    }
}
