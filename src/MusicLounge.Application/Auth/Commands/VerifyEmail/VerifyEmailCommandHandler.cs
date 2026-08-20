using MediatR;
using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Auth.Commands.VerifyEmail;

internal sealed class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, AuthResultDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuthAttemptTracker _authAttemptTracker;

    public VerifyEmailCommandHandler(
        IUnitOfWork uow, IJwtTokenService jwtTokenService, IAuthAttemptTracker authAttemptTracker)
    {
        _uow = uow;
        _jwtTokenService = jwtTokenService;
        _authAttemptTracker = authAttemptTracker;
    }

    public async Task<AuthResultDto> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var users = await userRepo.FindAsync(u => u.Email == request.Email, ct);
        var user = users.FirstOrDefault();

        if (user is not null)
        {
            if (user.EmailVerifiedAt is not null)
                throw new ConflictException("Tài khoản đã được xác thực, vui lòng đăng nhập.");

            var lockoutRemaining = await _authAttemptTracker.GetLockoutRemainingAsync(user.Id, ct);
            if (lockoutRemaining is not null)
                throw new UnauthorizedException(
                    $"Tài khoản tạm thời bị khóa do nhập sai mã xác thực nhiều lần. Vui lòng thử lại sau {Math.Ceiling(lockoutRemaining.Value.TotalMinutes)} phút.");
        }

        // Luon hash request.Code du email co ton tai hay khong — tranh lo timing side-channel cho
        // phep do email da dang ky hay chua (OWASP ASVS V2.1 - account enumeration), giong pattern
        // da dung o LoginCommandHandler cho mat khau.
        var codeHash = PasswordResetTokenHasher.Hash(request.Code);
        var codeValid = user?.EmailVerificationCodeHash is not null && user.EmailVerificationCodeHash == codeHash;

        if (user is null || !codeValid)
        {
            if (user is not null)
                await _authAttemptTracker.RecordFailureAsync(user.Id, ct);
            throw new UnauthorizedException("Email hoặc mã xác thực không đúng.");
        }

        if (user.EmailVerificationCodeExpiresAt is null || user.EmailVerificationCodeExpiresAt < DateTimeOffset.UtcNow)
            throw new UnauthorizedException("Mã xác thực đã hết hạn. Vui lòng yêu cầu gửi lại mã.");

        await _authAttemptTracker.ResetAsync(user.Id, ct);

        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        user.EmailVerificationCodeHash = null;
        user.EmailVerificationCodeExpiresAt = null;
        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        int? loungeId = null;
        if (user.Role == UserRole.Staff)
        {
            var staffAssignments = await _uow.Repository<LoungeStaff, int>()
                .FindAsync(s => s.UserId == user.Id && s.IsActive, ct);
            loungeId = staffAssignments.FirstOrDefault()?.LoungeId;
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user, loungeId);

        return new AuthResultDto(token, expiresAt, user.Id, user.Email, user.FullName, user.Role.ToString(), loungeId);
    }
}
