using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Donations;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// D4 Chặng 2 overdue checks — donation the venue has received but not yet paid to the performer
/// (PerformerPaid): past the payout due date → donation_pending reminder to Owner (once); past twice
/// the hold → venue_penalties(Warning) (W30/D4), plus a penalty_warning notification.
///
/// <para>MLACP-362: cả hai mốc tính từ lúc phòng trà <b>thật sự nhận tiền</b>
/// (<see cref="DonationPayoutDeadline"/>), không phải từ lúc chủ bấm "đã nhận". Trước đây là từ lúc bấm
/// (hardcode 7/14 ngày), và vì tự xác nhận đặt lại mốc đó nên chủ im lặng được thêm 7 ngày so với chủ
/// xác nhận ngay. Số ngày giờ đọc từ <c>donation_hold_days</c> — cùng một cấu hình với hạn tự xác nhận,
/// lịch sử của chủ và điều kiện khiếu nại.</para>
/// </summary>
public sealed class DonationOverdueCheckJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;

    public DonationOverdueCheckJob(
        ApplicationDbContext ctx, IUnitOfWork uow, ISystemConfigService config, INotificationService notifications)
    {
        _ctx = ctx;
        _uow = uow;
        _config = config;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;
        var holdDays = await DonationPayoutDeadline.HoldDaysAsync(_config, ct);

        // This job was unreachable until MLACP's DI fix (missing registration), so it had never
        // actually been exercised. Dates are compared client-side — same SQLite-translation
        // limitation documented throughout this codebase.
        var received = await _ctx.Donations
            .Where(d => d.Status == DonationStatus.OwnerReceived)
            .ToListAsync(ct);
        if (received.Count == 0) return;

        var releaseTimes = await DonationPayoutDeadline.PayoutReleaseTimesAsync(
            _uow, received.Select(d => d.Id).ToList(), ct);

        int? systemAdminId = null;

        foreach (var donation in received)
        {
            var receivedAt = DonationPayoutDeadline.ReceivedAt(donation, releaseTimes);
            if (DonationPayoutDeadline.DueAt(receivedAt, holdDays) is not { } dueAt || now < dueAt) continue;

            var info = await GetOwnershipAsync(donation.Id, ct);
            if (info is null) continue;

            if (now >= DonationPayoutDeadline.WarningAt(receivedAt, holdDays))
            {
                var evidenceRef = $"donation:{donation.Id}";
                var alreadyPenalized = await _ctx.VenuePenalties
                    .AnyAsync(p => p.EvidenceRef == evidenceRef, ct);

                if (!alreadyPenalized)
                {
                    systemAdminId ??= await _ctx.Users
                        .Where(u => u.Role == UserRole.Admin)
                        .Select(u => (int?)u.Id)
                        .FirstOrDefaultAsync(ct);

                    if (systemAdminId is int adminId)
                    {
                        _ctx.VenuePenalties.Add(new VenuePenalty
                        {
                            LoungeId = info.Value.LoungeId,
                            PenaltyType = PenaltyType.Warning,
                            Reason = $"Donate #{donation.Id} quá {2 * holdDays} ngày kể từ khi phòng trà nhận tiền " +
                                     "vẫn chưa trả nghệ sĩ.",
                            EvidenceRef = evidenceRef,
                            IssuedBy = adminId,
                            IssuedAt = now,
                            EffectiveAt = now,
                            Status = PenaltyStatus.Active
                        });

                        await _notifications.NotifyAsync(
                            info.Value.OwnerId,
                            NotificationType.PenaltyWarning,
                            "Cảnh báo vi phạm",
                            $"Phòng trà của bạn bị cảnh báo vì donate #{donation.Id} đã quá {2 * holdDays} ngày " +
                            "kể từ khi nhận tiền mà chưa chuyển cho nghệ sĩ.",
                            referenceType: "donation",
                            referenceId: donation.Id.ToString(),
                            ct: ct);
                    }
                }
            }
            else
            {
                var alreadyReminded = await _ctx.Notifications.AnyAsync(
                    n => n.UserId == info.Value.OwnerId
                        && n.Type == NotificationType.DonationPending
                        && n.ReferenceType == "donation"
                        && n.ReferenceId == donation.Id.ToString(), ct);
                if (alreadyReminded) continue;

                await _notifications.NotifyAsync(
                    info.Value.OwnerId,
                    NotificationType.DonationPending,
                    "Nhắc nhở: chưa trả nghệ sĩ",
                    $"Donate #{donation.Id} đã tới hạn chuyển cho nghệ sĩ ({VietnamTime.Format(dueAt)}) — " +
                    "vui lòng chuyển khoản cho nghệ sĩ.",
                    referenceType: "donation",
                    referenceId: donation.Id.ToString(),
                    ct: ct);
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }

    private async Task<(int OwnerId, int LoungeId)?> GetOwnershipAsync(int donationId, CancellationToken ct)
    {
        var row = await _ctx.Donations
            .Where(d => d.Id == donationId)
            .Select(d => new { d.Performance.LoungeShow.Lounge.OwnerId, d.Performance.LoungeShow.LoungeId })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : (row.OwnerId, row.LoungeId);
    }
}
