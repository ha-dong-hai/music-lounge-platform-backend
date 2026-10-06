using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
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
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<SendPerformerConfirmationEmailJob> _logger;

    public SendPerformerConfirmationEmailJob(
        IEmailService emailService, ISecretProtector secretProtector, IFileStorageService fileStorage,
        ILogger<SendPerformerConfirmationEmailJob> logger)
    {
        _emailService = emailService;
        _secretProtector = secretProtector;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public Task ExecuteAsync(
        string toEmail, string toName, string subjectVi, string subjectEn, string messageVi, string messageEn,
        string protectedLink, DateTimeOffset expiresAt, CancellationToken ct = default)
        => _emailService.SendPerformerConfirmationAsync(
            toEmail, toName, new SongNgu(subjectVi, subjectEn), new SongNgu(messageVi, messageEn),
            _secretProtector.Unprotect(protectedLink), expiresAt, attachment: null, ct);

    /// <summary>
    /// MLACP-673. Thư "phòng trà báo đã chuyển tiền ủng hộ" kèm ẢNH CHỨNG TỪ phòng trà đã nộp, để nghệ sĩ đối chiếu với
    /// sao kê ngân hàng của mình trước khi bấm xác nhận. Trước đây thư chỉ có số tiền và mã chuyển khoản — chứng từ chỉ
    /// phòng trà và nền tảng thấy, đúng người cần nó nhất lại không.
    ///
    /// <para>Ảnh được đọc LÚC GỬI từ kho tệp của hệ thống (không lưu bản sao trong kho job — ảnh chứng từ có số tài khoản).
    /// Chứng từ là liên kết ngoài hệ thống thì KHÔNG tải về (tránh SSRF, cùng luật với ConfirmDonationPaid) — thư in liên
    /// kết đó. Không đọc được ảnh (tệp đã mất) thì vẫn gửi thư không đính kèm: liên kết xác nhận mới là việc chính, và
    /// trang xác nhận vẫn hiện chứng từ.</para>
    /// </summary>
    public async Task ExecuteWithEvidenceAsync(
        string toEmail, string toName, string subjectVi, string subjectEn, string messageVi, string messageEn,
        string protectedLink, DateTimeOffset expiresAt, string? evidenceUrl, CancellationToken ct = default)
    {
        EmailAttachment? dinhKem = null;
        if (!string.IsNullOrWhiteSpace(evidenceUrl))
        {
            if (_fileStorage.IsOwnUploadUrl(evidenceUrl))
            {
                try
                {
                    var bytes = await _fileStorage.ReadPublicImageAsync(evidenceUrl, ct);
                    var mime = ImageMimeTypeHelper.FromContent(bytes);
                    if (mime is not null && mime.StartsWith("image/", StringComparison.Ordinal))
                        dinhKem = new EmailAttachment("chung-tu-chuyen-khoan." + mime["image/".Length..], mime, bytes);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Không đọc được ảnh chứng từ để đính kèm thư cho nghệ sĩ — gửi thư không đính kèm.");
                }
            }
            else
            {
                messageVi += $" Chứng từ phòng trà gửi kèm: {evidenceUrl}";
                messageEn += $" Proof of transfer provided by the venue: {evidenceUrl}";
            }
        }

        await _emailService.SendPerformerConfirmationAsync(
            toEmail, toName, new SongNgu(subjectVi, subjectEn), new SongNgu(messageVi, messageEn),
            _secretProtector.Unprotect(protectedLink), expiresAt, dinhKem, ct);
    }
}
