using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Notifications.Queries.GetUnreadNotificationCount;

public sealed record GetUnreadNotificationCountQuery : IQuery<int>;
