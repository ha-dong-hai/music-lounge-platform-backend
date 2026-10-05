using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Application.Performers.Jobs;

/// <summary>
/// MLACP-642. Gửi liên kết tự xác nhận cho nghệ sĩ NGOÀI lệnh đã tạo ra nó.
///
/// <para><b>Trước đây</b> <see cref="PerformerConfirmations.InviteAsync"/> gửi thư ngay trong lệnh — tức bên trong giao
/// dịch ghi sổ của "phòng trà báo đã trả nghệ sĩ" (ConfirmDonationPaid) — và dùng token huỷ của request. Đo 05/10/2026 trên
/// Gmail thật: mỗi lần báo đã trả mất 4–5 giây; người dùng đóng tab giữa chừng thì request bị huỷ (499), việc gửi thư ném
/// OperationCanceledException và CẢ giao dịch quay lui — lời xác nhận trả tiền mất, không ai biết.</para>
///
/// <para>Hangfire gọi lớp này (không gọi thẳng IEmailService) để liên kết — chứa token một lần, ai cầm cũng trả lời thay
/// nghệ sĩ được — chỉ ở dạng đã mã hoá trong kho job, cùng lý do với <c>SendPasswordResetEmailJob</c>.
/// Hai thành phần song ngữ được truyền thành chuỗi riêng: tham số job được lưu dạng JSON, chuỗi trần là dạng chắc chắn
/// đọc lại được qua các lần đổi kiểu.</para>
/// </summary>
public sealed class SendPerformerConfirmationEmailJob
{
    private readonly IEmailService _emailService;
    private readonly ISecretProtector _secretProtector;

    public SendPerformerConfirmationEmailJob(IEmailService emailService, ISecretProtector secretProtector)
    {
        _emailService = emailService;
        _secretProtector = secretProtector;
    }

    public Task ExecuteAsync(
        string toEmail, string toName, string subjectVi, string subjectEn, string messageVi, string messageEn,
        string protectedLink, DateTimeOffset expiresAt, CancellationToken ct = default)
        => _emailService.SendPerformerConfirmationAsync(
            toEmail, toName, new SongNgu(subjectVi, subjectEn), new SongNgu(messageVi, messageEn),
            _secretProtector.Unprotect(protectedLink), expiresAt, ct);
}
