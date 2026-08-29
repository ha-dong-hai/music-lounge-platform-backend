using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;

namespace MusicLounge.Application.Notifications.Queries.GetUnreadNotificationCount;

internal sealed class GetUnreadNotificationCountQueryHandler
    : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly INotificationRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetUnreadNotificationCountQueryHandler(INotificationRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken ct)
        => _repo.GetUnreadCountAsync(_currentUser.UserId, ct);
}
