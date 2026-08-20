using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// Detects a User.Role changing to Admin outside the application entirely — there is currently no
/// API path to promote a user to Admin at all, so today the ONLY way an account becomes Admin is a
/// direct database edit. Comparing the current Role=Admin set against a persisted baseline
/// (KnownAdminSnapshot) catches that regardless of how it happened, without needing an audit trail
/// on a promotion command that doesn't exist yet.
/// </summary>
public sealed class AdminRoleDriftDetectionJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;

    public AdminRoleDriftDetectionJob(ApplicationDbContext ctx, INotificationService notifications)
    {
        _ctx = ctx;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;

        var currentAdmins = await _ctx.Users
            .Where(u => u.Role == UserRole.Admin)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);
        var knownSnapshots = await _ctx.KnownAdminSnapshots.ToListAsync(ct);

        // First run ever: no baseline exists yet, so every current Admin is only "new" relative to
        // an empty table, not relative to reality — silently establish the baseline instead of
        // alerting on every admin that already legitimately existed before this job shipped.
        if (knownSnapshots.Count == 0)
        {
            if (currentAdmins.Count == 0) return;
            foreach (var admin in currentAdmins)
                _ctx.KnownAdminSnapshots.Add(new KnownAdminSnapshot
                {
                    UserId = admin.Id,
                    FirstDetectedAt = DateTimeOffset.UtcNow
                });
            await _ctx.SaveChangesAsync(ct);
            return;
        }

        var knownIds = knownSnapshots.Select(s => s.UserId).ToHashSet();
        var currentIds = currentAdmins.Select(a => a.Id).ToHashSet();
        var newAdmins = currentAdmins.Where(a => !knownIds.Contains(a.Id)).ToList();
        var demoted = knownSnapshots.Where(s => !currentIds.Contains(s.UserId)).ToList();

        if (newAdmins.Count > 0)
        {
            foreach (var newAdmin in newAdmins)
            {
                foreach (var admin in currentAdmins)
                {
                    await _notifications.NotifyAsync(
                        admin.Id,
                        NotificationType.SecurityAlert,
                        "Cảnh báo bảo mật: phát hiện tài khoản Admin mới",
                        $"Tài khoản \"{newAdmin.Email}\" (UserId={newAdmin.Id}) vừa được phát hiện có quyền " +
                        "Admin mà hệ thống chưa từng ghi nhận trước đó. Hệ thống hiện chưa có chức năng cấp " +
                        "quyền Admin qua ứng dụng, nên mọi thay đổi role đều đến từ thao tác trực tiếp trên " +
                        "cơ sở dữ liệu — nếu đây không phải do bạn thực hiện, vui lòng kiểm tra ngay.",
                        referenceType: "user",
                        referenceId: newAdmin.Id.ToString(),
                        ct: ct);
                }

                _ctx.KnownAdminSnapshots.Add(new KnownAdminSnapshot
                {
                    UserId = newAdmin.Id,
                    FirstDetectedAt = DateTimeOffset.UtcNow
                });
            }
        }

        // No longer Admin — drop from the baseline so a FUTURE re-promotion of this same account
        // gets flagged again too, rather than being silently trusted forever from one past snapshot.
        if (demoted.Count > 0)
            _ctx.KnownAdminSnapshots.RemoveRange(demoted);

        if (newAdmins.Count > 0 || demoted.Count > 0)
            await _ctx.SaveChangesAsync(ct);
    }
}
