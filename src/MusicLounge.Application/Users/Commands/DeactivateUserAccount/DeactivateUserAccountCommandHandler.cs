using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.DeactivateUserAccount;

internal sealed class DeactivateUserAccountCommandHandler : IRequestHandler<DeactivateUserAccountCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<DeactivateUserAccountCommandHandler> _logger;

    public DeactivateUserAccountCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ILogger<DeactivateUserAccountCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeactivateUserAccountCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        user.IsActive = false;
        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        // LoggingBehavior only logs the command type name, not who/what — for an action that bans
        // a user (VenuePenalty already has this pattern via IssuedBy/Reason for venue penalties;
        // account ban/unban had no equivalent trail at all).
        _logger.LogWarning(
            "User account deactivated: TargetUserId={TargetUserId} by AdminUserId={AdminUserId} at {At}",
            request.UserId, _currentUser.UserId, DateTimeOffset.UtcNow);

        return Unit.Value;
    }
}
