using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// Detects credential stuffing: the same source IP failing login across many DIFFERENT accounts
/// in a short window — a pattern IAuthAttemptTracker's per-account lockout can't see, since it only
/// ever looks at one account at a time. Alert-only (never auto-blocks an IP), same posture as the
/// moderation/complaint SLA breach jobs: a human decides what to do with a suspected attacker,
/// this job's job is only to make sure a human actually notices.
/// Thresholds come from SecurityDetectionSettings (appsettings), not system_config — see that
/// class's comment for why these specifically are kept out of the DB-editable config table.
/// </summary>
public sealed class LoginSpikeDetectionJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;
    private readonly SecurityDetectionSettings _settings;

    public LoginSpikeDetectionJob(
        ApplicationDbContext ctx, INotificationService notifications, IOptions<SecurityDetectionSettings> settings)
    {
        _ctx = ctx;
        _notifications = notifications;
        _settings = settings.Value;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-_settings.LoginSpikeWindowMinutes);
        var alertCooldown = TimeSpan.FromHours(_settings.LoginSpikeAlertCooldownHours);

        // Any DateTimeOffset comparison translated into the SQL itself fails against this test
        // suite's SQLite provider (not just combined-with-equality, as documented elsewhere in this
        // codebase — a lone ">=" fails here too) — materialize first, then filter client-side.
        var allFailures = await _ctx.LoginFailureLogs.ToListAsync(ct);
        var recentFailures = allFailures.Where(l => l.CreatedAt >= windowStart).ToList();

        var suspiciousIps = recentFailures
            .Where(l => l.IpAddress is not null)
            .GroupBy(l => l.IpAddress!)
            .Where(g => g.Count() >= _settings.LoginSpikeMinTotalAttempts
                && g.Select(x => x.Email).Distinct().Count() >= _settings.LoginSpikeMinDistinctAccounts)
            .ToList();

        if (suspiciousIps.Count > 0)
        {
            var admins = await _ctx.Users.Where(u => u.Role == UserRole.Admin).ToListAsync(ct);

            foreach (var group in suspiciousIps)
            {
                var ip = group.Key;
                var alertState = await _ctx.LoginSpikeAlertStates.FirstOrDefaultAsync(s => s.IpAddress == ip, ct);
                if (alertState is not null && now - alertState.LastAlertedAt < alertCooldown)
                    continue; // Already alerted recently for this IP — an ongoing attack shouldn't spam every run.

                var distinctAccounts = group.Select(x => x.Email).Distinct().Count();
                foreach (var admin in admins)
                {
                    await _notifications.NotifyAsync(
                        admin.Id,
                        NotificationType.SecurityAlert,
                        "Cảnh báo bảo mật: nghi ngờ tấn công dò mật khẩu hàng loạt",
                        $"Phát hiện {group.Count()} lượt đăng nhập thất bại trên {distinctAccounts} tài khoản " +
                        $"khác nhau từ cùng một địa chỉ IP ({ip}) trong {_settings.LoginSpikeWindowMinutes} phút qua " +
                        "— có thể là tấn công dò mật khẩu hàng loạt (credential stuffing). Vui lòng kiểm tra.",
                        referenceType: "security_ip",
                        referenceId: ip,
                        ct: ct);
                }

                if (alertState is null)
                    _ctx.LoginSpikeAlertStates.Add(new LoginSpikeAlertState { IpAddress = ip, LastAlertedAt = now });
                else
                    alertState.LastAlertedAt = now;

                await _ctx.SaveChangesAsync(ct);
            }
        }

        // Retention: this table only needs to cover LoginSpikeWindowMinutes of history to do its
        // job — prune anything older so it doesn't grow unbounded on a busy site. Reuses
        // allFailures (already materialized above) instead of a second DateTimeOffset-filtered
        // round trip.
        var cutoff = now.Subtract(TimeSpan.FromHours(_settings.LoginFailureLogRetentionHours));
        var stale = allFailures.Where(l => l.CreatedAt < cutoff).ToList();
        if (stale.Count > 0)
        {
            _ctx.LoginFailureLogs.RemoveRange(stale);
            await _ctx.SaveChangesAsync(ct);
        }
    }
}
