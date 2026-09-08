using System.Security.Cryptography;
using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Auth.Commands.ResendVerificationCode;

internal sealed class ResendVerificationCodeCommandHandler : IRequestHandler<ResendVerificationCodeCommand, Unit>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    // Chong spam email: khoang cach toi thieu giua 2 lan gui, suy ra tu chinh
    // EmailVerificationCodeExpiresAt da co san (= lan gui truoc + CodeLifetime) thay vi them cot
    // DB moi rieng cho muc dich nay.
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly IUnitOfWork _uow;
    private readonly IBackgroundJobService _backgroundJobs;

    public ResendVerificationCodeCommandHandler(IUnitOfWork uow, IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<Unit> Handle(ResendVerificationCodeCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var users = await userRepo.FindAsync(u => u.Email == request.Email, ct);
        var user = users.FirstOrDefault();

        // Anti-enumeration: luon tra ve thanh cong du email khong ton tai, da xac thuc roi, tai
        // khoan bi khoa, hay dang trong cooldown — khong lam gi ca trong cac truong hop do, giong
        // het pattern cua ForgotPasswordCommandHandler.
        var lastSentAt = user?.EmailVerificationCodeExpiresAt?.Subtract(CodeLifetime);
        var inCooldown = lastSentAt is not null && DateTimeOffset.UtcNow - lastSentAt < ResendCooldown;

        if (user is not null && user.IsActive && user.EmailVerifiedAt is null && !inCooldown)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

            user.EmailVerificationCodeHash = PasswordResetTokenHasher.Hash(code);
            user.EmailVerificationCodeExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);
            userRepo.Update(user);
            await _uow.SaveChangesAsync(ct);

            _backgroundJobs.EnqueueEmailVerificationCode(user.Email, user.FullName, code);
        }

        return Unit.Value;
    }
}
