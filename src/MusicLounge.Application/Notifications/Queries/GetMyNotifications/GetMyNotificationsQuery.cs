using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Notifications.DTOs;

namespace MusicLounge.Application.Notifications.Queries.GetMyNotifications;

public sealed record GetMyNotificationsQuery(int Page, int PageSize) : IQuery<PaginatedResult<NotificationDto>>;
