using MediatR;
using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Auth.Commands.GoogleLogin;

internal sealed class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, AuthResultDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IGoogleTokenVerifier _googleTokenVerifier;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISystemConfigService _config;

    public GoogleLoginCommandHandler(
        IUnitOfWork uow,
        IGoogleTokenVerifier googleTokenVerifier,
        IJwtTokenService jwtTokenService,
        ISystemConfigService config)
    {
        _uow = uow;
        _googleTokenVerifier = googleTokenVerifier;
        _jwtTokenService = jwtTokenService;
        _config = config;
    }

    public async Task<AuthResultDto> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        var googleInfo = await _googleTokenVerifier.VerifyAsync(request.IdToken, ct);

        var userRepo = _uow.Repository<User, int>();

        var byGoogleId = await userRepo.FindAsync(u => u.GoogleId == googleInfo.GoogleId, ct);
        var user = byGoogleId.FirstOrDefault();

        if (user is null)
        {
            var byEmail = await userRepo.FindAsync(u => u.Email == googleInfo.Email, ct);
            user = byEmail.FirstOrDefault();

            if (user is not null)
            {
                // Classic-Federated Merge Attack (OWASP account pre-hijacking): if this local
                // account was never verified, its PasswordHash may belong to an attacker who
                // pre-registered the victim's email with a password of their own choosing —
                // LoginCommandHandler blocks that attacker from logging in only because
                // EmailVerifiedAt is still null. Silently setting EmailVerifiedAt here (because
                // Google vouches for the email) without touching PasswordHash would remove that
                // block and hand the attacker a fully working login for the real owner's account.
                // Discard the untrusted password; the real owner still gets in via Google right
                // now, and can set a fresh password later via ForgotPassword if they want it too.
                var wasUnverified = user.EmailVerifiedAt is null;

                // Link existing local account to Google — email đã được Google xác thực hộ.
                user.GoogleId = googleInfo.GoogleId;
                user.EmailVerifiedAt ??= DateTimeOffset.UtcNow;
                if (wasUnverified)
                    user.PasswordHash = null;
                userRepo.Update(user);
            }
            else
            {
                // Luật 91/2025/QH15 lawful-basis requirement — same as local registration. Only
                // enforced here, in the branch that actually creates a new User row; an existing
                // account (found by GoogleId or linked by email above) doesn't need to re-consent.
                if (!request.AcceptTerms)
                    throw new DomainException(
                        "Bạn cần đồng ý với Điều khoản dịch vụ và Chính sách bảo mật để đăng ký.");

                var termsVersion = await _config.GetStringAsync(
                    ConfigKeys.CurrentTermsVersion, ConfigKeys.CurrentTermsVersionDefault, ct);

                user = new User
                {
                    Email = googleInfo.Email,
                    FullName = googleInfo.FullName,
                    AvatarUrl = googleInfo.AvatarUrl,
                    GoogleId = googleInfo.GoogleId,
                    AuthProvider = "google",
                    PasswordHash = null,
                    EmailVerifiedAt = DateTimeOffset.UtcNow,
                    TermsAcceptedAt = DateTimeOffset.UtcNow,
                    TermsVersion = termsVersion,
                    CreatedAt = DateTime.UtcNow
                };
                userRepo.Add(user);
            }

            await _uow.SaveChangesAsync(ct);
        }

        if (!user.IsActive)
            throw new UnauthorizedException("Tài khoản đã bị khóa do vi phạm quy định sử dụng. Vui lòng liên hệ hỗ trợ nếu bạn cho rằng đây là nhầm lẫn.");

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
