using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Notifications.Commands.MarkNotificationRead;

internal sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationReadCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<Notification, int>();
        var notification = await repo.GetByIdAsync(request.NotificationId, ct)
            ?? throw new NotFoundException(nameof(Notification), request.NotificationId);

        if (notification.UserId != _currentUser.UserId)
            throw new ForbiddenException("Thông báo này không thuộc về bạn.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            repo.Update(notification);
            await _uow.SaveChangesAsync(ct);
        }

        return Unit.Value;
    }
}
