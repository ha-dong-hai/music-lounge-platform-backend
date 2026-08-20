using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.ReactivateUserAccount;

internal sealed class ReactivateUserAccountCommandHandler : IRequestHandler<ReactivateUserAccountCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ReactivateUserAccountCommandHandler> _logger;

    public ReactivateUserAccountCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ILogger<ReactivateUserAccountCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Unit> Handle(ReactivateUserAccountCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        user.IsActive = true;
        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "User account reactivated: TargetUserId={TargetUserId} by AdminUserId={AdminUserId} at {At}",
            request.UserId, _currentUser.UserId, DateTimeOffset.UtcNow);

        return Unit.Value;
    }
}
