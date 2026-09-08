using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Notifications.Commands.UnregisterDeviceToken;

internal sealed class UnregisterDeviceTokenCommandHandler : IRequestHandler<UnregisterDeviceTokenCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UnregisterDeviceTokenCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UnregisterDeviceTokenCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<DeviceToken, int>();
        var existing = (await repo.FindAsync(
            t => t.Token == request.Token && t.UserId == _currentUser.UserId, ct)).FirstOrDefault();

        // Idempotent: logout can race a token already cleared by another session/device — no error
        // either way, the end state (this token no longer targets this user) is what the caller wants.
        if (existing is not null)
        {
            repo.Remove(existing);
            await _uow.SaveChangesAsync(ct);
        }

        return Unit.Value;
    }
}
