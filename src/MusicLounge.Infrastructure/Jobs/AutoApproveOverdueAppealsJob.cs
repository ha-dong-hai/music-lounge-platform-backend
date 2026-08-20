using Microsoft.EntityFrameworkCore;
using Hangfire;
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

        List<User>? admins = null;

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

            // A venue can have more than one Suspension/Ban in effect at once — only reset to
            // Approved if no OTHER currently-in-effect penalty still justifies keeping it locked.
            var hasOtherActivePenalty = await _ctx.VenuePenalties.AnyAsync(
                p => p.LoungeId == current.LoungeId
                    && p.Id != current.Id
                    && p.Status == PenaltyStatus.Active
                    && p.AppliedAt != null
                    && (p.PenaltyType == PenaltyType.Suspension || p.PenaltyType == PenaltyType.Ban),
                ct);
            if (!hasOtherActivePenalty)
                lounge.Status = LoungeStatus.Approved;

            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.AppealResolved,
                "Kháng cáo tự động được chấp thuận",
                $"Admin không xử lý kháng cáo cho phạt #{current.Id} trong thời hạn SLA — kháng cáo được " +
                "tự động chấp thuận, venue trở lại hoạt động bình thường.",
                referenceType: "venue_penalty",
                referenceId: current.Id.ToString(),
                ct: ct);

            if (wasAlreadyApplied)
            {
                // Same reasoning as ReviewAppealCommandHandler: subscription compensation already
                // applied by ApplyDuePenaltiesJob needs a human to reverse correctly.
                admins ??= await _ctx.Users.Where(u => u.Role == UserRole.Admin).ToListAsync(ct);
                foreach (var admin in admins)
                {
                    await _notifications.NotifyAsync(
                        admin.Id,
                        NotificationType.AppealResolved,
                        "Cần xử lý thủ công: hoàn tác bù trừ subscription",
                        $"Phạt #{current.Id} ({current.PenaltyType}) trên \"{lounge.Name}\" đã tự động " +
                        "overturn (quá hạn SLA) sau khi bù trừ subscription đã áp dụng. Vui lòng kiểm " +
                        "tra và điều chỉnh owner_subscriptions/ledger thủ công cho đúng.",
                        referenceType: "venue_penalty",
                        referenceId: current.Id.ToString(),
                        ct: ct);
                }
            }

            await _ctx.SaveChangesAsync(ct);
        }
    }
}
