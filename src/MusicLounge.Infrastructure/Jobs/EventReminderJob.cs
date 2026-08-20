using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>W23 — reminds ticket holders shortly before their show starts.</summary>
public sealed class EventReminderJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;

    public EventReminderJob(ApplicationDbContext ctx, ISystemConfigService config, INotificationService notifications)
    {
        _ctx = ctx;
        _config = config;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var reminderHours = await _config.GetIntAsync(ConfigKeys.EventReminderHours, 24, ct);
        var now = DateTimeOffset.UtcNow;
        var windowEnd = now.AddHours(reminderHours);

        // Reminder window is a couple of hours wide (not a single instant) so an hourly job
        // reliably catches every show without needing a persisted "already reminded" flag —
        // instead we just check whether a reminder Notification already exists per (show, buyer).
        // Same SQLite-translation limitation documented throughout this codebase — even two pure
        // DateTimeOffset comparisons ANDed together in one Where fail to translate under the
        // SQLite provider used in tests. Filter by Status server-side, the date range client-side.
        var dueShows = (await _ctx.LoungeShows
                .Where(s => s.Status == LoungeShowStatus.Published || s.Status == LoungeShowStatus.Ongoing)
                .ToListAsync(ct))
            .Where(s => s.ScheduledStart > now && s.ScheduledStart <= windowEnd)
            .ToList();

        foreach (var show in dueShows)
        {
            var buyerIds = await _ctx.Tickets
                .Where(t => t.ShowId == show.Id && t.Status == TicketStatus.Confirmed && t.BuyerId != null)
                .Select(t => t.BuyerId!.Value)
                .Distinct()
                .ToListAsync(ct);

            foreach (var buyerId in buyerIds)
            {
                var alreadyReminded = await _ctx.Notifications.AnyAsync(
                    n => n.UserId == buyerId
                        && n.Type == NotificationType.EventReminder
                        && n.ReferenceType == "show"
                        && n.ReferenceId == show.Id.ToString(), ct);
                if (alreadyReminded) continue;

                await _notifications.NotifyAsync(
                    buyerId,
                    NotificationType.EventReminder,
                    "Sắp đến giờ diễn!",
                    $"\"{show.Name}\" sẽ bắt đầu lúc {show.ScheduledStart:HH:mm dd/MM/yyyy}.",
                    referenceType: "show",
                    referenceId: show.Id.ToString(),
                    ct: ct);
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
