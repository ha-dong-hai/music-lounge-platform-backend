namespace MusicLounge.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string toEmail, string toName, string resetLink, CancellationToken ct = default);

    Task SendEmailVerificationCodeAsync(
        string toEmail, string toName, string code, CancellationToken ct = default);

    // Sent to the REAL existing account's email when someone attempts to register with an
    // already-used address — see RegisterCommandHandler's anti-enumeration design (OWASP
    // Authentication Cheat Sheet: registration must not reveal whether an email is taken).
    Task SendDuplicateRegistrationAlertEmailAsync(
        string toEmail, string toName, CancellationToken ct = default);
}
