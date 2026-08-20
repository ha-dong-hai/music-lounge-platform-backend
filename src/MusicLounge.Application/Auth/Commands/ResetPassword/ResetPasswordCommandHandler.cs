using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Auth.Commands.ResetPassword;

internal sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordCommandHandler(IUnitOfWork uow, IPasswordHasher passwordHasher)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var tokenHash = PasswordResetTokenHasher.Hash(request.Token);
        var userRepo = _uow.Repository<User, int>();
        var users = await userRepo.FindAsync(u => u.PasswordResetTokenHash == tokenHash, ct);
        var user = users.FirstOrDefault();

        if (user is null || user.PasswordResetTokenExpiresAt is null ||
            user.PasswordResetTokenExpiresAt < DateTimeOffset.UtcNow)
            throw new UnauthorizedException(
                "Link đặt lại mật khẩu không hợp lệ hoặc đã hết hạn (link chỉ dùng được 1 lần trong thời gian giới hạn). Vui lòng yêu cầu gửi lại link mới.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        // Xoa token ngay sau khi dung — token 1 lan, khong the tai su dung du chua het han.
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        // Rotate the security stamp so every JWT issued before this reset — e.g. one an attacker
        // who prompted the reset already stole — fails OnTokenValidated on its very next request,
        // instead of staying valid for up to AccessTokenExpiryMinutes more.
        user.SecurityStamp = Guid.NewGuid();

        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
