using MediatR;
using MusicLounge.Application.Auth;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.VerifyPhone;

internal sealed class VerifyPhoneCommandHandler : IRequestHandler<VerifyPhoneCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthAttemptTracker _authAttemptTracker;

    public VerifyPhoneCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IAuthAttemptTracker authAttemptTracker)
    {
        _uow = uow;
        _currentUser = currentUser;
        _authAttemptTracker = authAttemptTracker;
    }

    public async Task<Unit> Handle(VerifyPhoneCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        if (user.PhoneVerified)
            throw new ConflictException("Số điện thoại đã được xác thực.");

        var lockoutRemaining = await _authAttemptTracker.GetLockoutRemainingAsync(user.Id, ct);
        if (lockoutRemaining is not null)
            throw new UnauthorizedException(
                $"Tài khoản tạm thời bị khóa do nhập sai mã xác thực nhiều lần. Vui lòng thử lại sau {Math.Ceiling(lockoutRemaining.Value.TotalMinutes)} phút.");

        var codeHash = PasswordResetTokenHasher.Hash(request.Code);
        var codeValid = user.PhoneVerificationCodeHash is not null && user.PhoneVerificationCodeHash == codeHash;

        if (!codeValid)
        {
            await _authAttemptTracker.RecordFailureAsync(user.Id, ct);
            throw new UnauthorizedException("Mã xác thực không đúng.");
        }

        if (user.PhoneVerificationCodeExpiresAt is null || user.PhoneVerificationCodeExpiresAt < DateTimeOffset.UtcNow)
            throw new UnauthorizedException("Mã xác thực đã hết hạn. Vui lòng yêu cầu gửi lại mã.");

        await _authAttemptTracker.ResetAsync(user.Id, ct);

        user.PhoneVerified = true;
        user.PhoneVerificationCodeHash = null;
        user.PhoneVerificationCodeExpiresAt = null;
        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
