using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common;
using MusicLounge.Application.Interfaces;

namespace MusicLounge.Infrastructure.Securities;

public class SmtpEmailSenderService : IEmailSenderService
{
    private readonly EmailSettings _emailSettings;

    public SmtpEmailSenderService(IOptions<EmailSettings> emailOptions)
    {
        _emailSettings = emailOptions.Value;
    }

    public async Task SendVerificationCode(string toEmail, string fullName, string verificationCode, int expiredMinutes)
    {
        if (string.IsNullOrWhiteSpace(_emailSettings.Host)
            || string.IsNullOrWhiteSpace(_emailSettings.FromEmail)
            || string.IsNullOrWhiteSpace(_emailSettings.Password))
        {
            throw new InvalidOperationException("Thiếu cấu hình MailSettings để gửi email xác thực");
        }

        using var message = new MailMessage();
        message.From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
        message.To.Add(toEmail);
        message.Subject = "Ma xac thuc dang ky tai khoan Music Lounge";
        message.IsBodyHtml = true;
        message.Body = BuildVerificationBody(fullName, verificationCode, expiredMinutes);

        using var client = new SmtpClient(_emailSettings.Host, _emailSettings.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_emailSettings.FromEmail, _emailSettings.Password)
        };

        await client.SendMailAsync(message);
    }

    private static string BuildVerificationBody(string fullName, string verificationCode, int expiredMinutes)
    {
        return $@"
            <div style='font-family:Arial,sans-serif;line-height:1.6'>
                <h2>Music Lounge</h2>
                <p>Xin chao {WebUtility.HtmlEncode(fullName)},</p>
                <p>Ma xac thuc dang ky tai khoan cua ban la:</p>
                <div style='font-size:28px;font-weight:bold;letter-spacing:6px;color:#1f4b99'>{verificationCode}</div>
                <p>Ma co hieu luc trong {expiredMinutes} phut.</p>
                <p>Neu ban khong thuc hien thao tac nay, vui long bo qua email.</p>
            </div>";
    }
}
