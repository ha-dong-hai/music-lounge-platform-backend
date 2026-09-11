namespace MusicLounge.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string toEmail, string toName, string resetLink, CancellationToken ct = default);

    Task SendEmailVerificationCodeAsync(
        string toEmail, string toName, string code, CancellationToken ct = default);

    // MLACP-364: lien ket mot lan de nghe si tu xac nhan tai khoan nhan tien / da nhan donate.
    Task SendPerformerConfirmationAsync(
        string toEmail, string toName, string subject, string message, string link,
        DateTimeOffset expiresAt, CancellationToken ct = default);
}
