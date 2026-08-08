using System.Security.Cryptography;
using MediatR;
using MusicLounge.Application.Auth.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Auth.Commands.Register;

internal sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResultDto>
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IBackgroundJobService _backgroundJobs;

    public RegisterCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<RegisterResultDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        var emailExists = await _uow.Repository<User, int>()
            .AnyAsync(u => u.Email == request.Email, ct);
        if (emailExists)
            throw new ConflictException("Email này đã được đăng ký. Vui lòng đăng nhập hoặc dùng email khác.");

        var code = GenerateVerificationCode();
        var expiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);

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
