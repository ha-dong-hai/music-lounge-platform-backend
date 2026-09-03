using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
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
        // ActiveUserBehavior + Program.cs's OnTokenValidated revoke access the instant IsActive
        // flips false, mid-session — a mistaken self-deactivation or deactivating the last Admin
        // would lock all admin access with no in-app recovery path.
        if (request.UserId == _currentUser.UserId)
            throw new DomainException("Không thể tự khoá tài khoản của chính mình.");

        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        if (user.Role == UserRole.Admin)
        {
            var otherActiveAdmins = await userRepo.CountAsync(
                u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id, ct);
            if (otherActiveAdmins == 0)
                throw new DomainException("Không thể khoá Admin cuối cùng còn hoạt động.");
        }

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
