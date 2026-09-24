using MusicLounge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Moderations;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// MLACP-222 / NĐ 147/2024: nội dung bị người dùng báo cáo vi phạm có SLA gỡ bỏ (mặc định 48h —
/// ContentReportSlaHours, khác với ModerationSlaHours 24h dùng cho cổng duyệt AI trước khi đăng).
/// Mirrors ModerationSlaBreachAlertJob's shape — cùng cách tính "quá hạn", cùng cách chỉ cảnh báo
/// (không tự động quyết định thay Admin), cùng không dedupe giữa các lần chạy (Admin vẫn nhận
/// cảnh báo mỗi giờ cho tới khi xử lý xong — mirror hành vi hiện có của job kia).
/// </summary>
public sealed class ContentReportSlaBreachAlertJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;
    private readonly ISystemConfigService _config;

    public ContentReportSlaBreachAlertJob(
        ApplicationDbContext ctx, INotificationService notifications, ISystemConfigService config)
    {
        _ctx = ctx;
        _notifications = notifications;
        _config = config;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;
        var slaHours = await ContentReportSla.SlaHoursAsync(_config, ct);

        var openReports = await _ctx.ContentReports
            .Where(r => r.Status == ContentReportStatus.Open)
            .ToListAsync(ct);
        if (openReports.Count == 0) return;

        var breachedGroups = openReports
            .GroupBy(r => (r.TargetType, r.TargetId))
            .Select(g => new
            {
                g.Key.TargetType,
                g.Key.TargetId,
                ReportCount = g.Count(),
                EarliestReportedAt = g.Min(r => r.CreatedAt)
            })
            .Where(g => g.EarliestReportedAt.AddHours(slaHours) <= now)
            .ToList();
        if (breachedGroups.Count == 0) return;

        var admins = await _ctx.Users.Where(u => u.Role == UserRole.Admin).ToListAsync(ct);
        if (admins.Count == 0) return;

        // MLACP-414: khong gui lai canh bao moi gio cho cung mot dich. Canh bao o day gom theo DICH bao cao chu khong
        // theo tung bao cao, nen chong trung phai kem moc thoi gian: chi bo qua khi canh bao cu duoc tao SAU bao cao
        // som nhat cua dot dang mo. Nho vay mot dot bao cao moi (sau khi dot cu da duoc xu ly) van duoc canh bao lai,
        // thay vi im lang vinh vien cho dich do.
        var breachedRefs = breachedGroups.Select(g => $"{g.TargetType}:{g.TargetId}").ToList();
        var previousAlerts = (await _ctx.Notifications
                .Where(n => n.Type == NotificationType.ContentReportSlaBreached
                            && n.ReferenceType == "content_report_target"
                            && n.ReferenceId != null
                            && breachedRefs.Contains(n.ReferenceId))
                .Select(n => new { n.UserId, n.ReferenceId, n.CreatedAt })
                .ToListAsync(ct))
            .ToList();

        foreach (var group in breachedGroups)
        {
            var reference = $"{group.TargetType}:{group.TargetId}";
            var hoursOverdue = (int)(now - group.EarliestReportedAt.AddHours(slaHours)).TotalHours;
            foreach (var admin in admins)
            {
                if (previousAlerts.Any(n => n.UserId == admin.Id && n.ReferenceId == reference
                                            && n.CreatedAt >= group.EarliestReportedAt))
                    continue;

                await _notifications.NotifyAsync(
                    admin.Id,
                    NotificationType.ContentReportSlaBreached,
                    new SongNgu(
                        "Quá hạn xử lý báo cáo vi phạm (NĐ 147/2024)",
                        "Violation report overdue (Decree 147/2024)"),
                    new SongNgu(
                        $"{group.TargetType} #{group.TargetId} có {group.ReportCount} báo cáo, đã quá hạn " +
                        $"{hoursOverdue}h mà chưa được xử lý. Vui lòng xử lý ngay.",
                        $"{group.TargetType} #{group.TargetId} has {group.ReportCount} reports and is " +
                        $"{hoursOverdue}h overdue without being handled. Please handle it now."),
                    referenceType: "content_report_target",
                    referenceId: $"{group.TargetType}:{group.TargetId}",
                    ct: ct);
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
