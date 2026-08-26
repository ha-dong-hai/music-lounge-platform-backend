using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.DomainEventHandlers;

internal sealed class SendFcmConfirmHandler : INotificationHandler<TicketPaymentConfirmed>
{
    private readonly INotificationService _notifications;

    public SendFcmConfirmHandler(INotificationService notifications) => _notifications = notifications;

    public Task Handle(TicketPaymentConfirmed notification, CancellationToken ct)
    {
        // Walk-in sales publish this event with UserId=0 (no buyer account) — nothing to notify.
        if (notification.UserId <= 0) return Task.CompletedTask;

        return _notifications.NotifyAsync(
            notification.UserId,
            NotificationType.TicketConfirmed,
            "Đặt vé thành công!",
            $"Bạn đã đặt {notification.TicketIds.Length} vé thành công. Kiểm tra mục Vé của tôi để xem chi tiết.",
            referenceType: "ticket",
            referenceId: notification.TicketIds.FirstOrDefault().ToString(),
            ct: ct);
    }
}
