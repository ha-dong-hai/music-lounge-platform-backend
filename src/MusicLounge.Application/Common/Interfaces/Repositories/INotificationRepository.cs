using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Notifications.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface INotificationRepository : IRepository<Notification, int>
{
    /// <param name="language">MLACP-489: "en" thì trả bản tiếng Anh (lùi về tiếng Việt ở dòng cũ chưa có bản dịch).</param>
    Task<PaginatedResult<NotificationDto>> GetMyNotificationsAsync(
        int userId, int page, int pageSize, string language, CancellationToken ct = default);

    Task MarkAllAsReadAsync(int userId, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default);
}
