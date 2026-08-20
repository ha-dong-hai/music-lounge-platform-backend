using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// NĐ 85/2021: platform must be the focal point for receiving/resolving consumer complaints.
/// Complaint.SlaDeadline (system_config complaint_sla_hours) previously didn't exist at all — the
/// channel was reachable but had no deadline tracking of any kind, unlike its EventModeration
/// cousin. Same alert-not-auto-decide posture as ModerationSlaBreachAlertJob: resolving a complaint
/// is a human judgment call (who's at fault, what remedy), not something safe to automate on a
/// timeout.
/// </summary>
public sealed class ComplaintSlaBreachAlertJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;

    public ComplaintSlaBreachAlertJob(ApplicationDbContext ctx, INotificationService notifications)
    {
        _ctx = ctx;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        var open = await _ctx.Complaints
            .Where(c => c.Status == ComplaintStatus.Open && c.SlaDeadline != null)
            .ToListAsync(ct);
        var breached = open.Where(c => c.SlaDeadline <= now).ToList();
        if (breached.Count == 0) return;

        var admins = await _ctx.Users.Where(u => u.Role == UserRole.Admin).ToListAsync(ct);
        if (admins.Count == 0) return;

        foreach (var complaint in breached)
        {
            var hoursOverdue = (int)(now - complaint.SlaDeadline!.Value).TotalHours;
            foreach (var admin in admins)
            {
                await _notifications.NotifyAsync(
                    admin.Id,
                    NotificationType.ComplaintUpdate,
                    "Quá hạn xử lý khiếu nại (NĐ 85/2021)",
                    $"Khiếu nại #{complaint.Id} ({complaint.Category}) đã quá hạn xử lý {hoursOverdue}h " +
                    "mà chưa có kết luận. Vui lòng xử lý ngay.",
                    referenceType: "complaint",
                    referenceId: complaint.Id.ToString(),
                    ct: ct);
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
