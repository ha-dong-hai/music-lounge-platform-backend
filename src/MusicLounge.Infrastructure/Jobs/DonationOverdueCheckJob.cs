using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// D4 Chặng 2 overdue checks — donation acknowledged by Owner (OwnerReceived) but not yet paid
/// to the performer (PerformerPaid): &gt;7 days → donation_pending reminder to Owner (once).
/// &gt;14 days → venue_penalties(Warning) (W30/D4), plus a penalty_warning notification.
/// </summary>
public sealed class DonationOverdueCheckJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;

    public DonationOverdueCheckJob(ApplicationDbContext ctx, INotificationService notifications)
    {
        _ctx = ctx;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var fourteenDaysAgo = now.AddDays(-14);

        // Combining the Status/OwnerAckAt-null-check predicates with the OwnerAckAt date
        // comparison in one Where doesn't translate under the SQLite provider used in tests —
        // filter server-side on the simple predicates, the date client-side (same limitation
        // documented throughout this codebase). This job was unreachable until now (missing DI
        // registration, fixed alongside this), so it had never actually been exercised.
        var overdue = (await _ctx.Donations
                .Where(d => d.Status == DonationStatus.OwnerReceived && d.OwnerAckAt != null)
                .ToListAsync(ct))
            .Where(d => d.OwnerAckAt <= sevenDaysAgo)
            .ToList();

        if (overdue.Count == 0) return;

        int? systemAdminId = null;

        foreach (var donation in overdue)
        {
            var info = await GetOwnershipAsync(donation.Id, ct);
            if (info is null) continue;

            var isFourteenPlus = donation.OwnerAckAt <= fourteenDaysAgo;

            if (isFourteenPlus)
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
                            Reason = $"Donate #{donation.Id} quá 14 ngày chưa trả nghệ sĩ.",
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
                            $"Phòng trà của bạn bị cảnh báo vì donate #{donation.Id} quá 14 ngày chưa chuyển cho nghệ sĩ.",
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
                    $"Donate #{donation.Id} đã quá 7 ngày kể từ khi bạn xác nhận nhận tiền — vui lòng chuyển khoản cho nghệ sĩ.",
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
