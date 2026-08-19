using System.Security.Cryptography;
using MediatR;
using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Auth.Commands.Register;

internal sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResultDto>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IBackgroundJobService _backgroundJobs;
    private readonly ISystemConfigService _config;

    public RegisterCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IBackgroundJobService backgroundJobs,
        ISystemConfigService config)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _backgroundJobs = backgroundJobs;
        _config = config;
    }

    public async Task<RegisterResultDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        // Anti-enumeration (OWASP Authentication Cheat Sheet): registration must not reveal
        // whether an email is already taken — an attacker could otherwise harvest which emails
        // have MusicLounge accounts just by watching the response. On a duplicate, the REAL
        // account owner gets an alert email instead (so a genuine account-takeover attempt is
        // still visible to them), and the caller gets back the exact same response shape/content
        // a fresh registration would produce, built from what THEY submitted, never from the
        // existing account's real data (which would itself leak information back to the caller).
        var existingUsers = await _uow.Repository<User, int>()
            .FindAsync(u => u.Email == request.Email, ct);
        var existingUser = existingUsers.FirstOrDefault();
        if (existingUser is not null)
        {
            _backgroundJobs.EnqueueDuplicateRegistrationAlertEmail(existingUser.Email, existingUser.FullName);
            return new RegisterResultDto(
                request.Email, request.FullName, DateTimeOffset.UtcNow.Add(CodeLifetime));
        }

        var code = GenerateVerificationCode();
        var expiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);
        var now = DateTimeOffset.UtcNow;
        var termsVersion = await _config.GetStringAsync(
            ConfigKeys.CurrentTermsVersion, ConfigKeys.CurrentTermsVersionDefault, ct);

        var user = new User
        {
            Email = request.Email,
            FullName = request.FullName,
            Phone = request.Phone,
            PasswordHash = _passwordHasher.Hash(request.Password),
            AuthProvider = "local",
            Role = Enum.Parse<UserRole>(request.Role, ignoreCase: true),
            EmailVerificationCodeHash = PasswordResetTokenHasher.Hash(code),
            EmailVerificationCodeExpiresAt = expiresAt,
            // Validator already rejects AcceptTerms=false — this records WHEN and WHICH version,
            // not just that the checkbox existed.
            TermsAcceptedAt = now,
            TermsVersion = termsVersion,
            CreatedAt = DateTime.UtcNow
        };

        _uow.Repository<User, int>().Add(user);
        await _uow.SaveChangesAsync(ct);

        _backgroundJobs.EnqueueEmailVerificationCode(user.Email, user.FullName, code);

        return new RegisterResultDto(user.Email, user.FullName, expiresAt);
    }

    private static string GenerateVerificationCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
