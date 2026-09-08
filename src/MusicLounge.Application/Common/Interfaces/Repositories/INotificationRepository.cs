using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Notifications.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface INotificationRepository : IRepository<Notification, int>
{
    Task<PaginatedResult<NotificationDto>> GetMyNotificationsAsync(
        int userId, int page, int pageSize, CancellationToken ct = default);

    Task MarkAllAsReadAsync(int userId, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default);
}
