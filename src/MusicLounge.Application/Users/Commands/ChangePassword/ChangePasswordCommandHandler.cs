using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.ChangePassword;

internal sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;

    public ChangePasswordCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IPasswordHasher passwordHasher)
    {
        _uow = uow;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        if (user.PasswordHash is null)
            throw new DomainException("Tài khoản đăng nhập bằng Google, không có mật khẩu để đổi.");

        if (!_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
            throw new UnauthorizedException("Mật khẩu hiện tại không đúng.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        // Same reasoning as ResetPasswordCommandHandler: rotate so any JWT issued before this
        // change (e.g. one already leaked) stops working on its very next request instead of
        // staying valid for up to AccessTokenExpiryMinutes more.
        user.SecurityStamp = Guid.NewGuid();

        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
