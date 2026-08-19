using System.Security.Cryptography;
using MediatR;
using MusicLounge.Application.Auth;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.RequestChangeEmail;

internal sealed class RequestChangeEmailCommandHandler : IRequestHandler<RequestChangeEmailCommand, Unit>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobService _backgroundJobs;

    public RequestChangeEmailCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _currentUser = currentUser;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<Unit> Handle(RequestChangeEmailCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        if (string.Equals(request.NewEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Đây đã là email hiện tại của bạn.");

        var emailTaken = await userRepo.AnyAsync(
            u => u.Id != user.Id && u.Email == request.NewEmail, ct);
        if (emailTaken)
            throw new ConflictException("Email này đã được dùng bởi tài khoản khác.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        user.PendingEmail = request.NewEmail;
        user.EmailVerificationCodeHash = PasswordResetTokenHasher.Hash(code);
        user.EmailVerificationCodeExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);
        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        // Code goes to the NEW address — this is what proves the user actually controls it, the
        // same reason RequestPhoneVerificationCommandHandler sends its OTP to the phone being
        // verified rather than anywhere else.
        _backgroundJobs.EnqueueEmailVerificationCode(request.NewEmail, user.FullName, code);

        return Unit.Value;
    }
}
