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
    // MLACP-272: chong SMS-bombing (moi lan goi ban 1 SMS that) — khoang cach toi thieu giua 2 lan
    // gui, suy ra tu chinh PhoneVerificationCodeExpiresAt da co san (= lan gui truoc + CodeLifetime)
    // thay vi them cot DB moi, cung 1 ky thuat da dung o ResendVerificationCodeCommandHandler (email).
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

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

        // Khac voi ResendVerificationCodeCommandHandler (anti-enumeration, luon tra thanh cong im
        // lang), day la endpoint da xac thuc — nguoi dung tu yeu cau cho chinh minh, nen tra loi
        // ro rang thay vi im lang khi dang trong cooldown.
        var lastSentAt = user.PhoneVerificationCodeExpiresAt?.Subtract(CodeLifetime);
        if (lastSentAt is not null && DateTimeOffset.UtcNow - lastSentAt.Value < ResendCooldown)
        {
            var remaining = ResendCooldown - (DateTimeOffset.UtcNow - lastSentAt.Value);
            throw new DomainException(
                $"Vui lòng đợi {Math.Ceiling(remaining.TotalSeconds)} giây trước khi yêu cầu gửi lại mã.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        user.PhoneVerificationCodeHash = PasswordResetTokenHasher.Hash(code);
        user.PhoneVerificationCodeExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);
        userRepo.Update(user);
        await _uow.SaveChangesAsync(ct);

        _backgroundJobs.EnqueuePhoneVerificationCode(user.Phone, code);

        return Unit.Value;
    }
}
