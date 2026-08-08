using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

// Chuyen nhuong ve khong duoc accept/tu choi trong 48h thi tu dong huy, tra ve lai trang thai
// binh thuong cho chu cu (khong xoa ve, chi clear pending fields).
public sealed class TicketTransferExpiryJob
{
    private const int ExpiryHours = 48;

    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;

    public TicketTransferExpiryJob(ApplicationDbContext ctx, INotificationService notifications)
    {
        _ctx = ctx;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var cutoff = DateTimeOffset.UtcNow.AddHours(-ExpiryHours);

        // Same SQLite-translation limitation documented throughout this codebase: combining a
        // simple predicate (!= null) with a DateTimeOffset comparison in one query doesn't
        // translate — filter server-side on the simple predicate, then the date client-side.
        // Never caught before this audit — zero test coverage on this job.
        var pending = await _ctx.Tickets
            .Where(t => t.PendingTransferToUserId != null)
            .ToListAsync(ct);
        var expired = pending.Where(t => t.PendingTransferInitiatedAt <= cutoff).ToList();

        if (expired.Count == 0) return;

        foreach (var ticket in expired)
        {
            var recipientId = ticket.PendingTransferToUserId!.Value;
            ticket.PendingTransferToUserId = null;
            ticket.PendingTransferInitiatedAt = null;

            if (ticket.BuyerId is int senderId)
                await _notifications.NotifyAsync(
                    senderId, NotificationType.EventReminder,
                    "Yêu cầu chuyển nhượng vé đã hết hạn",
                    "Yêu cầu chuyển nhượng vé của bạn đã hết hạn sau 48 giờ vì người nhận chưa phản hồi.",
                    referenceType: "ticket", referenceId: ticket.Id.ToString(), ct: ct);

            await _notifications.NotifyAsync(
                recipientId, NotificationType.EventReminder,
                "Lời mời nhận vé đã hết hạn",
                "Lời mời nhận chuyển nhượng vé đã hết hạn vì bạn chưa phản hồi trong 48 giờ.",
                referenceType: "ticket", referenceId: ticket.Id.ToString(), ct: ct);
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
