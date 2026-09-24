using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Application.Common.Interfaces;

public interface IEmailService
{
    // MLACP-489: language = User.PreferredLanguage của người nhận ("vi" | "en").
    Task SendPasswordResetEmailAsync(
        string toEmail, string toName, string resetLink, string language, CancellationToken ct = default);

    Task SendEmailVerificationCodeAsync(
        string toEmail, string toName, string code, string language, CancellationToken ct = default);

    // MLACP-364: lien ket mot lan de nghe si tu xac nhan tai khoan nhan tien / da nhan donate.
    // MLACP-489: SongNgu vì nghệ sĩ không có tài khoản → không có ngôn ngữ ưa thích → thư gửi cả hai thứ tiếng.
    Task SendPerformerConfirmationAsync(
        string toEmail, string toName, SongNgu subject, SongNgu message, string link,
        DateTimeOffset expiresAt, CancellationToken ct = default);
}
