using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Auth.Commands.Login;

internal sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
{
    // Hash gia (khong khop bat ky mat khau that nao) dung khi email khong ton tai, de Verify() van
    // chay du chu ky PBKDF2 nhu truong hop email co that — tranh lo thoi gian phan hoi (timing
    // side-channel) cho phep do email da dang ky hay chua (OWASP ASVS V2.1 - account enumeration).
    private static string? _dummyHash;

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuthAttemptTracker _authAttemptTracker;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuthAttemptTracker authAttemptTracker,
        ILogger<LoginCommandHandler> logger)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _authAttemptTracker = authAttemptTracker;
        _logger = logger;
    }

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var users = await _uow.Repository<User, int>()
            .FindAsync(u => u.Email == request.Email, ct);
        var user = users.FirstOrDefault();

        if (user is not null)
        {
            var lockoutRemaining = await _authAttemptTracker.GetLockoutRemainingAsync(user.Id, ct);
            if (lockoutRemaining is not null)
            {
                _logger.LogWarning(
                    "Login rejected — account locked: UserId={UserId} at {At}", user.Id, DateTimeOffset.UtcNow);
                throw new UnauthorizedException(
                    $"Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau {Math.Ceiling(lockoutRemaining.Value.TotalMinutes)} phút.");
            }
        }

        _dummyHash ??= _passwordHasher.Hash("timing-normalization-dummy");
        var passwordValid = _passwordHasher.Verify(user?.PasswordHash ?? _dummyHash, request.Password);

        if (user is null || user.PasswordHash is null || !passwordValid)
        {
            if (user is not null)
            {
                await _authAttemptTracker.RecordFailureAsync(user.Id, ct);
                _logger.LogWarning(
                    "Login failed — wrong password: UserId={UserId} at {At}", user.Id, DateTimeOffset.UtcNow);
            }
            else
            {
                // Email logged (not sensitive, unlike the password) — lets an operator spot
                // credential-stuffing against a specific address even when it isn't registered.
                _logger.LogWarning(
                    "Login failed — unknown email: Email={Email} at {At}", request.Email, DateTimeOffset.UtcNow);
            }

            // Feeds LoginSpikeDetectionJob — distinct from IAuthAttemptTracker's per-account lockout
            // counter above, which only ever sees ONE account and can't detect the same source IP
            // failing across MANY different accounts (credential stuffing). LoginCommand is already
            // INoTransactionCommand, so this commits immediately regardless of the throw below.
            _uow.Repository<LoginFailureLog, int>().Add(new LoginFailureLog
            {
                Email = request.Email,
                IpAddress = request.IpAddress,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _uow.SaveChangesAsync(ct);

            throw new UnauthorizedException("Email hoặc mật khẩu không đúng.");
        }

        await _authAttemptTracker.ResetAsync(user.Id, ct);

        if (!user.IsActive)
            throw new UnauthorizedException("Tài khoản đã bị khóa do vi phạm quy định sử dụng. Vui lòng liên hệ hỗ trợ nếu bạn cho rằng đây là nhầm lẫn.");

        if (user.EmailVerifiedAt is null)
            throw new UnauthorizedException("Vui lòng xác thực email trước khi đăng nhập.");

        int? loungeId = null;
        if (user.Role == UserRole.Staff)
        {
            var staffAssignments = await _uow.Repository<LoungeStaff, int>()
                .FindAsync(s => s.UserId == user.Id && s.IsActive, ct);
            loungeId = staffAssignments.FirstOrDefault()?.LoungeId;
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user, loungeId);
        var (refreshToken, refreshExpiresAt) = _jwtTokenService.GenerateRefreshToken(user);

        _logger.LogInformation(
            "Login succeeded: UserId={UserId} Role={Role} at {At}", user.Id, user.Role, DateTimeOffset.UtcNow);

        return new AuthResultDto(
            token, expiresAt, user.Id, user.Email, user.FullName, user.Role.ToString(), loungeId,
            refreshToken, refreshExpiresAt);
    }
}
