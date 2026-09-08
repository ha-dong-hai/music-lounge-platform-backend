using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.DeactivateMyAccount;

internal sealed class DeactivateMyAccountCommandHandler : IRequestHandler<DeactivateMyAccountCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DeactivateMyAccountCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeactivateMyAccountCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        // MLACP-268: DeactivateUserAccountCommandHandler (Admin deactivating someone ELSE) already
        // blocks deactivating the last active Admin — this self-service route (DELETE /me) had no
        // equivalent guard, so the single remaining Admin could deactivate their OWN account and
        // lock the whole platform out of Admin access with no in-app recovery path at all (unlike
        // the Admin-target route, self-deactivation can't be "undone by another Admin" if there is
        // no other Admin left).
        if (user.Role == UserRole.Admin)
        {
            var otherActiveAdmins = await userRepo.CountAsync(
                u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id, ct);
            if (otherActiveAdmins == 0)
                throw new DomainException(
                    "Không thể khoá tài khoản của chính bạn — bạn là Admin cuối cùng còn hoạt động.");
        }

        user.IsActive = false;
        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
