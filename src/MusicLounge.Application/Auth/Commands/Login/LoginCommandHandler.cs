using MediatR;
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

    public LoginCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var users = await _uow.Repository<User, int>()
            .FindAsync(u => u.Email == request.Email, ct);
        var user = users.FirstOrDefault();

        _dummyHash ??= _passwordHasher.Hash("timing-normalization-dummy");
        var passwordValid = _passwordHasher.Verify(user?.PasswordHash ?? _dummyHash, request.Password);

        if (user is null || user.PasswordHash is null || !passwordValid)
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng.");

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

        return new AuthResultDto(token, expiresAt, user.Id, user.Email, user.FullName, user.Role.ToString(), loungeId);
    }
}
