using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Notifications.DTOs;

namespace MusicLounge.Application.Notifications.Queries.GetMyNotifications;

internal sealed class GetMyNotificationsQueryHandler
    : IRequestHandler<GetMyNotificationsQuery, PaginatedResult<NotificationDto>>
{
    private readonly INotificationRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetMyNotificationsQueryHandler(INotificationRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        return await _repo.GetMyNotificationsAsync(_currentUser.UserId, page, size, ct);
    }
}
