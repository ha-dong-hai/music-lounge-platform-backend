using MusicLounge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
///
/// MLACP-443: viec tu duyet do co the TAT bang system_config `appeal_auto_approve`. Khoa nay duoc
/// seed `true` ngay tu migration dau tien nhung truoc day khong noi nao doc — Admin tat no van thay
/// 200 OK va lich su thay doi duoc ghi, trong khi an phat van tiep tuc duoc go tu dong.
/// </summary>
public sealed class AutoApproveOverdueAppealsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;
    private readonly IAsyncKeyedLock _lock;
    private readonly ISystemConfigService _config;
    private readonly ILogger<AutoApproveOverdueAppealsJob> _logger;

    public AutoApproveOverdueAppealsJob(
        ApplicationDbContext ctx, INotificationService notifications, IAsyncKeyedLock @lock,
        ISystemConfigService config, ILogger<AutoApproveOverdueAppealsJob> logger)
    {
        _ctx = ctx;
        _notifications = notifications;
        _lock = @lock;
        _config = config;
        _logger = logger;
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

        // MLACP-443: kiem SAU khi da dem duoc so ho so qua han, khong phai truoc. Tat cong tac roi
        // im lang la cach mot chong ho so dong lai ma khong ai biet — chinh la thu cong tac nay sinh
        // ra de tranh. Moi lan chay ghi lai con so do de con thay ma xu ly tay.
        if (!await _config.GetBoolAsync(ConfigKeys.AppealAutoApprove, true, ct))
        {
            _logger.LogWarning(
                "Tu duyet khang cao dang TAT (system_config {ConfigKey}) — {Count} khang cao da qua han " +
                "SLA va dang cho Admin xu ly tay: phat #{PenaltyIds}.",
                ConfigKeys.AppealAutoApprove, overdue.Count, string.Join(", ", overdue.Select(p => p.Id)));
            return;
        }

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
                new SongNgu(
                    "Kháng cáo tự động được chấp thuận",
                    "Appeal automatically accepted"),
                new SongNgu(
                    $"Admin không xử lý kháng cáo cho phạt #{current.Id} trong thời hạn SLA — kháng cáo được " +
                    $"tự động chấp thuận. {PenaltyLifecycle.DescribeForOwner(lounge.Status)}".TrimEnd() +
                    (restoredPlan is null ? "" : $" Gói dịch vụ đã được kích hoạt lại, hết hạn {VietnamTime.Format(restoredPlan.ExpiresAt, "dd/MM/yyyy")}."),
                    $"The Admin did not handle the appeal against penalty #{current.Id} within the SLA — the appeal has been " +
                    $"accepted automatically. {PenaltyLifecycle.DescribeForOwnerEn(lounge.Status)}".TrimEnd() +
                    (restoredPlan is null
                        ? ""
                        : $" Your subscription has been reactivated and expires on {VietnamTime.Format(restoredPlan.ExpiresAt, "dd/MM/yyyy")}.")),
                referenceType: "venue_penalty",
                referenceId: current.Id.ToString(),
                ct: ct);

            await _ctx.SaveChangesAsync(ct);
        }
    }
}
