using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Notifications.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class NotificationRepository : Repository<Notification, int>, INotificationRepository
{
    private readonly ApplicationDbContext _ctx;

    public NotificationRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<PaginatedResult<NotificationDto>> GetMyNotificationsAsync(
        int userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _ctx.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.Title, n.Body, n.ReferenceType, n.ReferenceId, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<NotificationDto>(items, page, pageSize, total);
    }

    public async Task MarkAllAsReadAsync(int userId, CancellationToken ct = default)
    {
        await _ctx.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default)
        => _ctx.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && !n.IsRead, ct);
}
