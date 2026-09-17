using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Settings;
using MusicLounge.Application.Common;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// Gui email qua SMTP that khi da cau hinh Host (production). Chua cau hinh (Host rong — mac dinh
/// dev/self-host, giong pattern Cloudflare/VNPay stub cua du an) thi chi log ra console/file, khong
/// thu ket noi SMTP that — de tinh nang quen mat khau van chay full end-to-end trong dev/test ma
/// khong can credential ngoai. Doi sang nha cung cap that (SMTP relay/SendGrid/SES...) chi can dien
/// EmailSettings that, khong can sua code.
/// </summary>
internal sealed class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly IHostEnvironment _env;

    public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger, IHostEnvironment env)
    {
        _settings = settings.Value;
        _logger = logger;
        _env = env;
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail, string toName, string resetLink, CancellationToken ct = default)
    {
        var subject = "Đặt lại mật khẩu MusicLounge";
        var body = $"""
            Xin chào {toName},

            Bạn (hoặc ai đó) vừa yêu cầu đặt lại mật khẩu cho tài khoản MusicLounge của bạn.
            Nhấn vào link sau để đặt mật khẩu mới (link có hiệu lực trong 30 phút):

            {resetLink}

            Nếu bạn không yêu cầu điều này, hãy bỏ qua email này — mật khẩu của bạn vẫn an toàn.
            """;

        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            if (DuocGhiBiMat)
                _logger.LogWarning(
                    "EmailSettings:Host chưa cấu hình — không gửi email thật. Reset link cho {Email}: {ResetLink}",
                    toEmail, resetLink);
            else
                BaoKhongGui("đặt lại mật khẩu", toEmail);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, _settings.FromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(toEmail, toName));

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };

        await client.SendMailAsync(message, ct);
    }

    public async Task SendEmailVerificationCodeAsync(
        string toEmail, string toName, string code, CancellationToken ct = default)
    {
        var subject = "Xác thực email MusicLounge";
        var body = $"""
            Xin chào {toName},

            Mã xác thực email của bạn là: {code}

            Mã có hiệu lực trong 10 phút. Nhập mã này để hoàn tất đăng ký tài khoản MusicLounge.

            Nếu bạn không thực hiện đăng ký này, hãy bỏ qua email này.
            """;

        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            if (DuocGhiBiMat)
                _logger.LogWarning(
                    "EmailSettings:Host chưa cấu hình — không gửi email thật. Mã xác thực cho {Email}: {Code}",
                    toEmail, code);
            else
                BaoKhongGui("mã xác minh email", toEmail);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, _settings.FromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(toEmail, toName));

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };

        await client.SendMailAsync(message, ct);
    }

    public async Task SendPerformerConfirmationAsync(
        string toEmail, string toName, string subject, string message, string link,
        DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        var body = $"""
            Xin chào {toName},

            {message}

            {link}

            Liên kết chỉ dùng được một lần và hết hạn lúc {VietnamTime.Format(expiresAt, "HH:mm dd/MM/yyyy")} (giờ Việt Nam).
            Bạn không cần tạo tài khoản MusicLounge để trả lời.
            """;

        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            if (DuocGhiBiMat)
                _logger.LogWarning(
                    "EmailSettings:Host chưa cấu hình — không gửi email thật. Liên kết xác nhận cho {Email}: {ConfirmationLink}",
                    toEmail, link);
            else
                BaoKhongGui("mời nghệ sĩ xác nhận", toEmail);
            return;
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, _settings.FromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        mail.To.Add(new MailAddress(toEmail, toName));

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };

        await client.SendMailAsync(mail, ct);
    }

    /// <summary>
    /// MLACP-429. Ghi link/ma bi mat ra log CHI o Development va Testing: lap trinh vien khong co SMTP van lay duoc ma de
    /// thu, va 3 bo test (PerformerSelfConfirmation, PublicDonationStatement, DataEncryptedWithALostKey) doc link tu log.
    /// Moi truong khac ma thieu SMTP (vd cau hinh tren Azure bi mat) thi link dat lai mat khau nam trong log la ai doc
    /// log cung chiem duoc tai khoan — cung kieu loi voi ban gia SMS da sua o MLACP-426.
    /// </summary>
    private bool DuocGhiBiMat => _env.IsDevelopment() || _env.IsEnvironment("Testing");

    /// <summary>Error chu khong phai Warning: nguoi dung dang cho mot email se khong bao gio toi.</summary>
    private void BaoKhongGui(string loaiEmail, string toEmail)
        => _logger.LogError(
            "SMTP chưa cấu hình (Email:Host) — email {LoaiEmail} KHÔNG được gửi tới {Email}.", loaiEmail, CheEmail(toEmail));

    /// <summary>Giu 2 ky tu dau va ten mien — du de doi chieu khi ho tro nguoi dung, khong du de lo dia chi.</summary>
    internal static string CheEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var ten = email[..at];
        return (ten.Length <= 2 ? ten[..1] : ten[..2]) + "***" + email[at..];
    }
}
