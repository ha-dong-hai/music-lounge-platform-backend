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

    public VerifyEmailCommandHandler(IUnitOfWork uow, IJwtTokenService jwtTokenService)
    {
        _uow = uow;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResultDto> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var users = await userRepo.FindAsync(u => u.Email == request.Email, ct);
        var user = users.FirstOrDefault()
            ?? throw new UnauthorizedException("Email hoặc mã xác thực không đúng.");

        if (user.EmailVerifiedAt is not null)
            throw new ConflictException("Tài khoản đã được xác thực, vui lòng đăng nhập.");

        var codeHash = PasswordResetTokenHasher.Hash(request.Code);
        if (user.EmailVerificationCodeHash is null || user.EmailVerificationCodeHash != codeHash)
            throw new UnauthorizedException("Mã xác thực không đúng.");

        if (user.EmailVerificationCodeExpiresAt is null || user.EmailVerificationCodeExpiresAt < DateTimeOffset.UtcNow)
            throw new UnauthorizedException("Mã xác thực đã hết hạn. Vui lòng yêu cầu gửi lại mã.");

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
