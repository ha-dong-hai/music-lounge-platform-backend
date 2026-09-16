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

    /// <summary>MLACP-414: tieu de nay la khoa chong gui trung — doi no thi canh bao cu khong con nhan ra duoc.</summary>
    private const string OverdueTitle = "Quá hạn xử lý khiếu nại (NĐ 85/2021)";

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

        // MLACP-414: job chay hang gio, nen neu khong nho da canh bao hay chua thi moi gio lai sinh them mot thong bao
        // cho cung mot khieu nai — tren Azure 16/09 Admin co 1.019 thong bao chua doc, gan nhu toan bo la mot khieu nai
        // duy nhat. Cung cach chong trung cua RefundSlaBreachAlertJob (MLACP-348): moi Admin nhan dung mot lan cho moi
        // khieu nai. Khoa theo ca Title vi ComplaintUpdate con dung cho cac thong bao khac cua khieu nai.
        var breachedIds = breached.Select(c => c.Id.ToString()).ToList();
        var alreadyAlerted = (await _ctx.Notifications
                .Where(n => n.Type == NotificationType.ComplaintUpdate
                            && n.ReferenceType == "complaint"
                            && n.Title == OverdueTitle
                            && n.ReferenceId != null
                            && breachedIds.Contains(n.ReferenceId))
                .Select(n => new { n.UserId, n.ReferenceId })
                .ToListAsync(ct))
            .Select(n => (n.UserId, n.ReferenceId))
            .ToHashSet();

        foreach (var complaint in breached)
        {
            var hoursOverdue = (int)(now - complaint.SlaDeadline!.Value).TotalHours;
            foreach (var admin in admins)
            {
                if (alreadyAlerted.Contains((admin.Id, complaint.Id.ToString()))) continue;

                await _notifications.NotifyAsync(
                    admin.Id,
                    NotificationType.ComplaintUpdate,
                    OverdueTitle,
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
