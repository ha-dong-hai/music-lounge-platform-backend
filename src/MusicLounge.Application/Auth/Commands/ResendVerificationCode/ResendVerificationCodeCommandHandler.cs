using System.Security.Cryptography;
using MediatR;
using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Auth.Commands.ResendVerificationCode;

internal sealed class ResendVerificationCodeCommandHandler
    : IRequestHandler<ResendVerificationCodeCommand, ResendVerificationCodeResultDto>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IUnitOfWork _uow;
    private readonly IBackgroundJobService _backgroundJobs;

    public ResendVerificationCodeCommandHandler(IUnitOfWork uow, IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<ResendVerificationCodeResultDto> Handle(ResendVerificationCodeCommand request, CancellationToken ct)
    {
        // Computed unconditionally, BEFORE checking whether a real account exists, so the returned
        // expiry looks identical whether or not anything was actually regenerated -- same
        // anti-enumeration shape RegisterCommandHandler already uses for its duplicate-email path.
        // Without this, a client could tell a real account apart from a non-existent one by whether
        // the countdown UI actually restarts.
        var expiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);

        var userRepo = _uow.Repository<User, int>();
        var users = await userRepo.FindAsync(u => u.Email == request.Email, ct);
        var user = users.FirstOrDefault();

        // Anti-enumeration: luon tra ve thanh cong du email khong ton tai hay da xac thuc roi —
        // khong lam gi ca trong 2 truong hop do, giong het pattern cua ForgotPasswordCommandHandler.
        if (user is not null && user.EmailVerifiedAt is null)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

            user.EmailVerificationCodeHash = PasswordResetTokenHasher.Hash(code);
            user.EmailVerificationCodeExpiresAt = expiresAt;
            userRepo.Update(user);
            await _uow.SaveChangesAsync(ct);

            _backgroundJobs.EnqueueEmailVerificationCode(user.Email, user.FullName, code);
        }

        return new ResendVerificationCodeResultDto(expiresAt);
    }
}
