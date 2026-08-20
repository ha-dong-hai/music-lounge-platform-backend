using System.Security.Cryptography;
using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Auth.Commands.ResendVerificationCode;

internal sealed class ResendVerificationCodeCommandHandler : IRequestHandler<ResendVerificationCodeCommand, Unit>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

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

        // Anti-enumeration: luon tra ve thanh cong du email khong ton tai hay da xac thuc roi —
        // khong lam gi ca trong 2 truong hop do, giong het pattern cua ForgotPasswordCommandHandler.
        if (user is not null && user.EmailVerifiedAt is null)
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
