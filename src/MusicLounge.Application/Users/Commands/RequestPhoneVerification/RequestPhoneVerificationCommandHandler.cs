using System.Security.Cryptography;
using MediatR;
using MusicLounge.Application.Auth;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Commands.RequestPhoneVerification;

internal sealed class RequestPhoneVerificationCommandHandler
    : IRequestHandler<RequestPhoneVerificationCommand, Unit>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobService _backgroundJobs;

    public RequestPhoneVerificationCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _currentUser = currentUser;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<Unit> Handle(RequestPhoneVerificationCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        if (string.IsNullOrWhiteSpace(user.Phone))
            throw new DomainException("Vui lòng cập nhật số điện thoại trong hồ sơ trước khi yêu cầu xác thực.");

        if (user.PhoneVerified)
            throw new ConflictException("Số điện thoại đã được xác thực.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        user.PhoneVerificationCodeHash = PasswordResetTokenHasher.Hash(code);
        user.PhoneVerificationCodeExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);
        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        _backgroundJobs.EnqueuePhoneVerificationCode(user.Phone, code);

        return Unit.Value;
    }
}
