using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Settings;

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

    public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
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
            _logger.LogWarning(
                "EmailSettings:Host chưa cấu hình — không gửi email thật. Reset link cho {Email}: {ResetLink}",
                toEmail, resetLink);
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
        _logger.LogInformation("Password reset email sent via SMTP to {Email}", toEmail);
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
            _logger.LogWarning(
                "EmailSettings:Host chưa cấu hình — không gửi email thật. Mã xác thực cho {Email}: {Code}",
                toEmail, code);
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
        _logger.LogInformation("Email verification code sent via SMTP to {Email}", toEmail);
    }

    public async Task SendDuplicateRegistrationAlertEmailAsync(
        string toEmail, string toName, CancellationToken ct = default)
    {
        var subject = "Có người vừa thử đăng ký bằng email của bạn — MusicLounge";
        var body = $"""
            Xin chào {toName},

            Ai đó vừa thử tạo tài khoản MusicLounge mới bằng địa chỉ email này — nhưng bạn đã có
            tài khoản rồi, nên chúng tôi không tạo tài khoản mới.

            Nếu đó là bạn, hãy đăng nhập bình thường, hoặc dùng chức năng "Quên mật khẩu" nếu bạn
            không nhớ mật khẩu.

            Nếu không phải bạn, bạn không cần làm gì cả — tài khoản của bạn vẫn an toàn, không có
            gì thay đổi.
            """;

        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            _logger.LogWarning(
                "EmailSettings:Host chưa cấu hình — không gửi email thật. Cảnh báo đăng ký trùng cho {Email}.",
                toEmail);
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
        _logger.LogInformation("Duplicate-registration alert email sent via SMTP to {Email}", toEmail);
    }
}
