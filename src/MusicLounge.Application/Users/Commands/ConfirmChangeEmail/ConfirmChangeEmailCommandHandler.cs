using MediatR;
using MusicLounge.Application.Auth;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.ConfirmChangeEmail;

internal sealed class ConfirmChangeEmailCommandHandler : IRequestHandler<ConfirmChangeEmailCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthAttemptTracker _authAttemptTracker;

    public ConfirmChangeEmailCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IAuthAttemptTracker authAttemptTracker)
    {
        _uow = uow;
        _currentUser = currentUser;
        _authAttemptTracker = authAttemptTracker;
    }

    public async Task<Unit> Handle(ConfirmChangeEmailCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        if (user.PendingEmail is null)
            throw new DomainException("Bạn chưa yêu cầu đổi email — gọi endpoint yêu cầu đổi email trước.");

        var lockoutRemaining = await _authAttemptTracker.GetLockoutRemainingAsync(user.Id, ct);
        if (lockoutRemaining is not null)
            throw new UnauthorizedException(
                $"Tài khoản tạm thời bị khóa do nhập sai mã xác thực nhiều lần. Vui lòng thử lại sau {Math.Ceiling(lockoutRemaining.Value.TotalMinutes)} phút.");

        var codeHash = PasswordResetTokenHasher.Hash(request.Code);
        var codeValid = user.EmailVerificationCodeHash is not null && user.EmailVerificationCodeHash == codeHash;

        if (!codeValid)
        {
            await _authAttemptTracker.RecordFailureAsync(user.Id, ct);
            throw new UnauthorizedException("Mã xác thực không đúng.");
        }

        if (user.EmailVerificationCodeExpiresAt is null || user.EmailVerificationCodeExpiresAt < DateTimeOffset.UtcNow)
            throw new UnauthorizedException("Mã xác thực đã hết hạn. Vui lòng yêu cầu gửi lại mã.");

        await _authAttemptTracker.ResetAsync(user.Id, ct);

        user.Email = user.PendingEmail;
        user.PendingEmail = null;
        user.EmailVerificationCodeHash = null;
        user.EmailVerificationCodeExpiresAt = null;
        // Email is a login credential (Login matches on Email+Password) — rotate so tokens issued
        // under the old address stop working, same reasoning as ChangePasswordCommandHandler.
        user.SecurityStamp = Guid.NewGuid();

        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
