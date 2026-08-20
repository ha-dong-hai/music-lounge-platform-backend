using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Auth.Commands.Logout;

internal sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public LogoutCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken ct)
    {
        var user = await _uow.Repository<User, int>().GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        // Rotating SecurityStamp is the whole mechanism — every access/refresh token already
        // issued embeds the OLD stamp, so ActiveUserBehavior/RefreshTokenCommandHandler's stamp
        // comparison starts failing for all of them immediately, without a token-revocation table.
        user.SecurityStamp = Guid.NewGuid();

        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
