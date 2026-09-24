using MusicLounge.Domain.ValueObjects;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces;

// Single write point for user notifications (mirrors ILedgerService's D8 pattern): persists the
// Notification row (in-app inbox) and enqueues the FCM push in one call, so no caller can do one
// without the other.
//
// MLACP-489: title/body là SongNgu, không phải string — mọi chỗ gọi buộc phải có bản tiếng Anh (xem SongNgu vì sao
// không dùng thêm tham số chuỗi). Không giữ overload nhận string: giữ thì chỗ gọi mới lại có đường viết một thứ tiếng.
public interface INotificationService
{
    Task NotifyAsync(
        int userId,
        NotificationType type,
        SongNgu title,
        SongNgu body,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken ct = default);
}
