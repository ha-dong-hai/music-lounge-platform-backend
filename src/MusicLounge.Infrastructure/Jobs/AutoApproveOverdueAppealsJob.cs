using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// §6.17 — Appeal SLA is 48h; if Admin hasn't resolved it by AppealDeadline, auto-approve
/// (Overturned) so an unattended appeal never leaves an Owner penalized indefinitely.
/// </summary>
public sealed class AutoApproveOverdueAppealsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;

    public AutoApproveOverdueAppealsJob(ApplicationDbContext ctx, INotificationService notifications)
    {
        _ctx = ctx;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var appealed = await _ctx.VenuePenalties
            .Where(p => p.Status == PenaltyStatus.Appealed && p.AppealDeadline != null)
            .ToListAsync(ct);
        var overdue = appealed.Where(p => p.AppealDeadline <= now).ToList();
        if (overdue.Count == 0) return;

        List<User>? admins = null;

        foreach (var penalty in overdue)
        {
            penalty.Status = PenaltyStatus.Overturned;
            penalty.AppealResult = "Overturned (auto — quá hạn SLA 48h)";
            penalty.ReviewedAt = now;

            var lounge = await _ctx.Lounges.FirstOrDefaultAsync(l => l.Id == penalty.LoungeId, ct);
            if (lounge is null) continue;

            var wasAlreadyApplied = penalty.EffectiveAt <= now
                && penalty.PenaltyType is PenaltyType.Suspension or PenaltyType.Ban;

            lounge.Status = LoungeStatus.Approved;

            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.AppealResolved,
                "Kháng cáo tự động được chấp thuận",
                $"Admin không xử lý kháng cáo cho phạt #{penalty.Id} trong 48h — kháng cáo được " +
                "tự động chấp thuận, venue trở lại hoạt động bình thường.",
                referenceType: "venue_penalty",
                referenceId: penalty.Id.ToString(),
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
                        $"Phạt #{penalty.Id} ({penalty.PenaltyType}) trên \"{lounge.Name}\" đã tự động " +
                        "overturn (quá hạn SLA) sau khi bù trừ subscription đã áp dụng. Vui lòng kiểm " +
                        "tra và điều chỉnh owner_subscriptions/ledger thủ công cho đúng.",
                        referenceType: "venue_penalty",
                        referenceId: penalty.Id.ToString(),
                        ct: ct);
                }
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
