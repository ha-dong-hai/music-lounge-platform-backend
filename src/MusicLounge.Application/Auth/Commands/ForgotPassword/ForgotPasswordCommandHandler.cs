using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Auth.Commands.ForgotPassword;

internal sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    private readonly IUnitOfWork _uow;
    private readonly IBackgroundJobService _backgroundJobs;
    private readonly BusinessSettings _businessSettings;

    public ForgotPasswordCommandHandler(
        IUnitOfWork uow,
        IBackgroundJobService backgroundJobs,
        IOptions<BusinessSettings> businessSettings)
    {
        _uow = uow;
        _backgroundJobs = backgroundJobs;
        _businessSettings = businessSettings.Value;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var users = await _uow.Repository<User, int>()
            .FindAsync(u => u.Email == request.Email, ct);
        var user = users.FirstOrDefault();

        // Luon tra ve thanh cong bat ke email co ton tai hay khong — tranh lo account enumeration
        // qua noi dung response. Gui email that (neu co) qua background job (Hangfire) thay vi
        // await inline, vua tranh timing side-channel do do tre goi SMTP that gay ra, vua khop
        // dung pattern IBackgroundJobService da dung cho FCM notification.
        if (user is not null)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            user.PasswordResetTokenHash = PasswordResetTokenHasher.Hash(rawToken);
            user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);

            _uow.Repository<User, int>().Update(user);
            await _uow.SaveChangesAsync(ct);

            var resetLink = $"{_businessSettings.PasswordResetUrl}?token={Uri.EscapeDataString(rawToken)}";
            _backgroundJobs.EnqueuePasswordResetEmail(user.Email, user.FullName, resetLink);
        }

        return Unit.Value;
    }
}
