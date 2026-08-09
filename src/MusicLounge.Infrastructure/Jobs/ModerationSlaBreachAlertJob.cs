using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// NĐ 147/2024: flagged content has a moderation SLA (default 24h, system_config
/// moderation_sla_hours), computed and stored on EventModeration.SlaDeadline at submission time —
/// but until this job, nothing anywhere ever compared that deadline to the current time. The field
/// was purely decorative: visible to an Admin who happened to eyeball the queue and do the date math
/// themselves, with no alert, no escalation, no way to notice a breach from outside the UI.
///
/// Deliberately does NOT auto-decide the moderation (unlike AutoApproveOverdueAppealsJob's
/// auto-approve-on-timeout, which is safe because it favors the party being penalized). Auto-
/// approving unreviewed content risks letting something genuinely non-compliant go live untouched;
/// auto-rejecting risks blocking a legitimate show/livestream on pure timeout with no review at all.
/// Both are real content-moderation decisions this job isn't positioned to make — it surfaces the
/// breach to every Admin instead, so a human decides.
/// </summary>
public sealed class ModerationSlaBreachAlertJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;

    public ModerationSlaBreachAlertJob(ApplicationDbContext ctx, INotificationService notifications)
    {
        _ctx = ctx;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        // Filter server-side on the simple predicates, the date comparison client-side — combining
        // a nullable-DateTimeOffset comparison with the other predicates in one Where doesn't
        // translate under the SQLite provider used in tests (same limitation documented throughout
        // this codebase's other jobs).
        var undecided = await _ctx.EventModerations
            .Where(m => m.AdminDecision == null && m.SlaDeadline != null)
            .ToListAsync(ct);
        var breached = undecided.Where(m => m.SlaDeadline <= now).ToList();
        if (breached.Count == 0) return;

        var admins = await _ctx.Users.Where(u => u.Role == UserRole.Admin).ToListAsync(ct);
        if (admins.Count == 0) return;

        foreach (var moderation in breached)
        {
            var hoursOverdue = (int)(now - moderation.SlaDeadline!.Value).TotalHours;
            foreach (var admin in admins)
            {
                await _notifications.NotifyAsync(
                    admin.Id,
                    NotificationType.ModerationSlaBreached,
                    "Quá hạn duyệt nội dung (NĐ 147/2024)",
                    $"{moderation.TargetType} #{moderation.TargetId} đã quá hạn SLA duyệt {hoursOverdue}h " +
                    "mà chưa có quyết định. Vui lòng xử lý ngay.",
                    referenceType: "event_moderation",
                    referenceId: moderation.Id.ToString(),
                    ct: ct);
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
