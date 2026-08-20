namespace MusicLounge.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string toEmail, string toName, string resetLink, CancellationToken ct = default);

    Task SendEmailVerificationCodeAsync(
        string toEmail, string toName, string code, CancellationToken ct = default);
}
