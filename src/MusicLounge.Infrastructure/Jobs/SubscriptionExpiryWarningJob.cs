using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

// D14: canh bao Owner truoc khi subscription het han o 3 moc 30/7/1 ngay.
public sealed class SubscriptionExpiryWarningJob
{
    private static readonly int[] WarningDays = [30, 7, 1];

    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;

    public SubscriptionExpiryWarningJob(ApplicationDbContext ctx, INotificationService notifications)
    {
        _ctx = ctx;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        // Combining the Status equality with the ExpiresAt comparison in one Where doesn't
        // translate under the SQLite provider used in tests — filter by Status server-side,
        // the date client-side (same limitation documented throughout this codebase).
        var activeSubs = (await _ctx.OwnerSubscriptions
                .Where(s => s.Status == SubscriptionStatus.Active)
                .ToListAsync(ct))
            .Where(s => s.ExpiresAt > now)
            .ToList();

        if (activeSubs.Count == 0) return;

        foreach (var sub in activeSubs)
        {
            var daysLeft = (sub.ExpiresAt - now).TotalDays;

            foreach (var milestone in WarningDays)
            {
                // Trong khoang [milestone-1, milestone] ngay - job chay hang ngay nen cua so 1 ngay
                // la du de khong bo sot moc nao.
                if (daysLeft > milestone || daysLeft <= milestone - 1) continue;

                var referenceId = $"{sub.Id}:{milestone}";
                var alreadySent = await _ctx.Notifications.AnyAsync(
                    n => n.UserId == sub.OwnerId
                        && n.Type == NotificationType.SubscriptionExpiring
                        && n.ReferenceType == "subscription"
                        && n.ReferenceId == referenceId, ct);
                if (alreadySent) continue;

                await _notifications.NotifyAsync(
                    sub.OwnerId,
                    NotificationType.SubscriptionExpiring,
                    "Gói subscription sắp hết hạn",
                    $"Gói subscription của bạn sẽ hết hạn trong {milestone} ngày nữa " +
                    $"({sub.ExpiresAt:dd/MM/yyyy}). Gia hạn để tiếp tục tạo event mới.",
                    referenceType: "subscription",
                    referenceId: referenceId,
                    ct: ct);

                break; // 1 subscription chi gui 1 canh bao gan nhat moi lan chay job
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
