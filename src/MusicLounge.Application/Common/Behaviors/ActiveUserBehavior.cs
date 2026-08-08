using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Common.Behaviors;

/// <summary>
/// Deactivating/banning a user (DeactivateMyAccountCommandHandler, DeactivateUserAccountCommandHandler)
/// only flips User.IsActive in the database — there is no server-side session/token revocation, and
/// IsActive is otherwise checked only once, at Login/GoogleLogin time. Without this behavior, a JWT
/// issued before deactivation stays cryptographically valid and fully authorized for its entire
/// remaining lifetime (AccessTokenExpiryMinutes, 60 minutes by default), so an Admin banning an
/// actively abusive/fraudulent user would not actually stop them for up to an hour. Re-checking
/// IsActive on every authenticated request closes that window.
/// </summary>
internal sealed class ActiveUserBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public ActiveUserBehavior(ICurrentUserService currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (_currentUser.IsAuthenticated)
        {
            var user = await _uow.Repository<User, int>().GetByIdAsync(_currentUser.UserId, ct);
            if (user is null || !user.IsActive)
                throw new UnauthorizedException("Tài khoản đã bị khóa hoặc không còn tồn tại.");
        }

        return await next();
    }
}
