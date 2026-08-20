using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces;

// Single write point for user notifications (mirrors ILedgerService's D8 pattern): persists the
// Notification row (in-app inbox) and enqueues the FCM push in one call, so no caller can do one
// without the other.
public interface INotificationService
{
    Task NotifyAsync(
        int userId,
        NotificationType type,
        string title,
        string body,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken ct = default);
}
