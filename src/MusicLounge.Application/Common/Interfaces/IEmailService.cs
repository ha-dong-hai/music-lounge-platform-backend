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
    // MLACP-673: attachment = ảnh chứng từ chuyển khoản phòng trà đã nộp (chỉ thư xác nhận đã nhận tiền ủng hộ).
    Task SendPerformerConfirmationAsync(
        string toEmail, string toName, SongNgu subject, SongNgu message, string link,
        DateTimeOffset expiresAt, EmailAttachment? attachment = null, CancellationToken ct = default);

    // MLACP-635: thư xác nhận vé sau khi thanh toán online thành công. Trước đó người mua chỉ nhận thông báo trong ứng
    // dụng — mất máy hoặc chưa cài ứng dụng thì không có gì trong tay chứng minh đã mua.
    Task SendTicketConfirmationAsync(TicketConfirmationEmail email, CancellationToken ct = default);
}

/// <summary>MLACP-635. Dữ liệu thư xác nhận vé — một thư cho MỘT lần thanh toán (có thể nhiều vé, nhiều hạng).</summary>
public sealed record TicketConfirmationEmail(
    string ToEmail,
    string ToName,
    string Language,
    string OrderCode,
    string ShowName,
    DateTimeOffset Start,
    DateTimeOffset End,
    string LoungeName,
    string? LoungeAddress,
    bool Online,
    IReadOnlyList<TicketConfirmationLine> Lines,
    decimal Total,
    string? TicketUrl);

public sealed record TicketConfirmationLine(string TierName, string? PriceName, int Quantity, decimal UnitPrice);

/// <summary>MLACP-673. Tệp đính kèm thư. Đính kèm chứ không nhúng ảnh vào thân thư: khung thư chung cố ý không có ảnh
/// (nhiều hộp thư chặn ảnh mặc định), còn tệp đính kèm thì hộp thư nào cũng hiện.</summary>
public sealed record EmailAttachment(string FileName, string MimeType, byte[] Content);
