using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Notifications.Commands.RegisterDeviceToken;

internal sealed class RegisterDeviceTokenCommandHandler : IRequestHandler<RegisterDeviceTokenCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public RegisterDeviceTokenCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RegisterDeviceTokenCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<DeviceToken, int>();
        var now = DateTimeOffset.UtcNow;

        // A token is unique per physical device, not per user — the same phone can log out of one
        // account and into another without FCM issuing a new token, so a stale row from the
        // previous owner must be reassigned rather than left to double-deliver push to both users.
        var existing = (await repo.FindAsync(t => t.Token == request.Token, ct)).FirstOrDefault();
        if (existing is not null)
        {
            existing.UserId = _currentUser.UserId;
            existing.Platform = request.Platform;
            existing.LastUsedAt = now;
            repo.Update(existing);
        }
        else
        {
            repo.Add(new DeviceToken
            {
                UserId = _currentUser.UserId,
                Token = request.Token,
                Platform = request.Platform,
                CreatedAt = now,
                LastUsedAt = now
            });
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
