using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Services.Email;
using MusicLounge.Infrastructure.Settings;
using MusicLounge.Application.Common;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// Gui email qua SMTP that khi da cau hinh Host (production). Chua cau hinh (Host rong — mac dinh
/// dev/self-host, giong pattern Cloudflare/VNPay stub cua du an) thi chi log ra console/file, khong
/// thu ket noi SMTP that — de tinh nang quen mat khau van chay full end-to-end trong dev/test ma
/// khong can credential ngoai. Doi sang nha cung cap that (SMTP relay/SendGrid/SES...) chi can dien
/// EmailSettings that, khong can sua code.
///
/// MLACP-635: moi thu dung chung khung <see cref="KhungThu"/> va gui HAI phan (text/plain + text/html, multipart/alternative).
/// Truoc do ca ba loai thu la chu tron khong khung, khong nut. Noi dung tung loai theo bo mau da duyet
/// (reports/Email - bộ mẫu thư gửi người dùng (bản đề xuất 05-10-2026).md).
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

    public Task SendPasswordResetEmailAsync(
        string toEmail, string toName, string resetLink, string language, CancellationToken ct = default)
    {
        if (!CoCachGui)
        {
            if (DuocGhiBiMat)
                _logger.LogWarning(
                    "EmailSettings:Host chưa cấu hình — không gửi email thật. Reset link cho {Email}: {ResetLink}",
                    toEmail, resetLink);
            else
                BaoKhongGui("đặt lại mật khẩu", toEmail);
            return Task.CompletedTask;
        }

        // MLACP-489: theo ngôn ngữ lưu trên tài khoản người nhận (email gửi bất đồng bộ, không có request để đọc).
        // Thư bảo mật: không in thông tin nào khác ngoài liên kết, và nói rõ "không yêu cầu thì không cần làm gì".
        var thu = new ThuEmail
        {
            NgonNgu = language,
            TieuDe = Theo(language, "Đặt lại mật khẩu", "Reset your password"),
            XemTruoc = Theo(language, "Liên kết dùng được một lần, hết hạn sau 30 phút.", "The link works once and expires in 30 minutes."),
            TenNguoiNhan = toName,
            DoanMo = [Theo(language,
                $"Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản MusicLounge {toEmail}. Bấm nút dưới đây để đặt mật khẩu mới. Liên kết có hiệu lực trong 30 phút và chỉ dùng được một lần.",
                $"We received a request to reset the password for the MusicLounge account {toEmail}. Use the button below to set a new password. The link is valid for 30 minutes and works only once.")],
            Nut = new NutThu(Theo(language, "Đặt mật khẩu mới", "Set a new password"), resetLink),
            DoanSau = [Theo(language,
                "Bạn không yêu cầu việc này? Mật khẩu của bạn vẫn an toàn, bạn không cần làm gì.",
                "Didn't ask for this? Your password is still safe and you don't need to do anything.")],
            LyDoNhan = Theo(language,
                "Bạn nhận thư này vì có yêu cầu đặt lại mật khẩu cho tài khoản dùng địa chỉ email này.",
                "You received this email because a password reset was requested for the account using this address."),
        };
        return GuiAsync(toEmail, toName, Theo(language, "Đặt lại mật khẩu MusicLounge", "Reset your MusicLounge password"), thu, ct);
    }

    public Task SendEmailVerificationCodeAsync(
        string toEmail, string toName, string code, string language, CancellationToken ct = default)
    {
        if (!CoCachGui)
        {
            if (DuocGhiBiMat)
                _logger.LogWarning(
                    "EmailSettings:Host chưa cấu hình — không gửi email thật. Mã xác thực cho {Email}: {Code}",
                    toEmail, code);
            else
                BaoKhongGui("mã xác minh email", toEmail);
            return Task.CompletedTask;
        }

        // MLACP-489: lúc đăng ký, ngôn ngữ này chính là ngôn ngữ của trang người dùng vừa điền form (xem
        // RegisterCommandHandler) — nên người đăng ký trên bản tiếng Anh nhận mã bằng tiếng Anh ngay từ thư đầu tiên.
        // Mã nằm cả ở tiêu đề thư: người dùng đọc được ngay trong thông báo của điện thoại, không phải mở thư.
        var thu = new ThuEmail
        {
            NgonNgu = language,
            TieuDe = Theo(language, "Xác thực email của bạn", "Verify your email"),
            XemTruoc = Theo(language, "Mã có hiệu lực 10 phút. Không chia sẻ mã này với ai.", "The code is valid for 10 minutes. Don't share it with anyone."),
            TenNguoiNhan = toName,
            DoanMo = [Theo(language, "Nhập mã dưới đây để hoàn tất đăng ký tài khoản MusicLounge.", "Enter the code below to finish creating your MusicLounge account.")],
            MaLon = code,
            DoanSau =
            [
                Theo(language, "Mã có hiệu lực trong 10 phút. MusicLounge không bao giờ hỏi mã này qua điện thoại hay tin nhắn.",
                    "The code is valid for 10 minutes. MusicLounge will never ask for this code by phone or message."),
                Theo(language, "Bạn không đăng ký? Hãy bỏ qua thư này, tài khoản sẽ không được kích hoạt.",
                    "Didn't sign up? Ignore this email and the account will not be activated."),
            ],
            LyDoNhan = Theo(language,
                "Bạn nhận thư này vì địa chỉ email này vừa được dùng để đăng ký tài khoản MusicLounge.",
                "You received this email because this address was just used to sign up for MusicLounge."),
        };
        return GuiAsync(toEmail, toName, Theo(language, $"Mã xác thực MusicLounge của bạn: {code}", $"Your MusicLounge verification code: {code}"), thu, ct);
    }

    public Task SendPerformerConfirmationAsync(
        string toEmail, string toName, SongNgu subject, SongNgu message, string link,
        DateTimeOffset expiresAt, EmailAttachment? attachment = null, CancellationToken ct = default)
    {
        if (!CoCachGui)
        {
            if (DuocGhiBiMat)
                _logger.LogWarning(
                    "EmailSettings:Host chưa cấu hình — không gửi email thật. Liên kết xác nhận cho {Email}: {ConfirmationLink}",
                    toEmail, link);
            else
                BaoKhongGui("mời nghệ sĩ xác nhận", toEmail);
            return Task.CompletedTask;
        }

        // MLACP-489: nghệ sĩ KHÔNG có tài khoản (do phòng trà quản lý), nên không có ngôn ngữ ưa thích nào để theo.
        // Thông lệ khi không biết ngôn ngữ người nhận: gửi cả hai trong một thư, tiếng Việt trước. Liên kết chỉ xuất
        // hiện MỘT lần (ở nút) — hai liên kết giống hệt nhau trong một thư dễ bị bộ lọc thư rác đánh dấu.
        var expires = VietnamTime.Format(expiresAt, "HH:mm dd/MM/yyyy");
        // MLACP-673: câu chỉ tới tệp đính kèm chỉ in khi thư THẬT SỰ có tệp — không hứa thứ không có trong thư.
        string[] chungTuVi = attachment is null ? [] :
            [$"Ảnh chứng từ chuyển khoản phòng trà đã nộp được đính kèm thư này ({attachment.FileName}). Hãy đối chiếu với sao kê tài khoản của bạn trước khi trả lời."];
        string[] chungTuEn = attachment is null ? [] :
            [$"The proof of transfer the venue submitted is attached to this email ({attachment.FileName}). Please check it against your bank statement before replying."];
        var thu = new ThuEmail
        {
            NgonNgu = NgonNgu.Viet,
            TieuDe = subject.Vi,
            XemTruoc = "Trả lời trong một lần bấm, không cần tạo tài khoản. / Reply in one click, no account needed.",
            TenNguoiNhan = toName,
            DoanMo =
            [
                message.Vi,
                .. chungTuVi,
                $"Liên kết chỉ dùng được một lần và hết hạn lúc {expires} (giờ Việt Nam). Bạn không cần tạo tài khoản MusicLounge để trả lời.",
            ],
            Nut = new NutThu("Xem và trả lời / View and reply", link),
            DoanSau =
            [
                "——— English ———",
                $"Hello {toName},",
                message.En,
                .. chungTuEn,
                $"Use the button above. It works only once and expires at {expires} (Vietnam time). You do not need a MusicLounge account to reply.",
            ],
            LyDoNhan = "Bạn nhận thư này vì một phòng trà trên MusicLounge ghi địa chỉ email này là liên hệ của bạn. / A venue on MusicLounge listed this address as your contact.",
        };
        return GuiAsync(toEmail, toName, $"{subject.Vi} / {subject.En}", thu, ct, attachment);
    }

    public Task SendTicketConfirmationAsync(TicketConfirmationEmail e, CancellationToken ct = default)
    {
        if (!CoCachGui)
        {
            // Thư vé không chứa bí mật (mã QR chỉ hiện trong trang Vé của tôi), nhưng email người nhận vẫn là dữ liệu cá nhân.
            if (DuocGhiBiMat)
                _logger.LogWarning("EmailSettings:Host chưa cấu hình — không gửi thư xác nhận vé {OrderCode} cho {Email}.", e.OrderCode, e.ToEmail);
            else
                BaoKhongGui("xác nhận vé", e.ToEmail);
            return Task.CompletedTask;
        }

        var l = e.Language;
        var gio = $"{ThuNgay(e.Start, l)}, {VietnamTime.Format(e.Start, "HH:mm")} – {VietnamTime.Format(e.End, "HH:mm")} {Theo(l, "(giờ Việt Nam)", "(Vietnam time)")}";
        var soVe = e.Lines.Sum(x => x.Quantity);

        var bang = new List<(string, string)>
        {
            (Theo(l, "Buổi diễn", "Show"), e.ShowName),
            (Theo(l, "Thời gian", "When"), gio),
            (Theo(l, "Nơi diễn", "Venue"), e.Online
                ? Theo(l, $"{e.LoungeName} — xem trực tuyến", $"{e.LoungeName} — online stream")
                : string.IsNullOrWhiteSpace(e.LoungeAddress) ? e.LoungeName : $"{e.LoungeName}, {e.LoungeAddress}"),
        };
        foreach (var x in e.Lines)
        {
            var hang = string.IsNullOrWhiteSpace(x.PriceName) || x.PriceName == x.TierName ? x.TierName : $"{x.TierName} · {x.PriceName}";
            bang.Add((Theo(l, "Hạng vé", "Ticket"), $"{hang} × {x.Quantity} — {Tien(x.UnitPrice * x.Quantity, l)}"));
        }
        bang.Add((Theo(l, "Tổng tiền", "Total"), Tien(e.Total, l)));
        bang.Add((Theo(l, "Mã đơn", "Order number"), e.OrderCode));

        var thu = new ThuEmail
        {
            NgonNgu = l,
            TieuDe = Theo(l, "Thanh toán thành công. Vé của bạn đã sẵn sàng.", "Payment received. Your tickets are ready."),
            XemTruoc = Theo(l, $"{ThuNgay(e.Start, l)} lúc {VietnamTime.Format(e.Start, "HH:mm")} tại {e.LoungeName}. Mã đơn {e.OrderCode}.",
                $"{ThuNgay(e.Start, l)} at {VietnamTime.Format(e.Start, "HH:mm")}, {e.LoungeName}. Order {e.OrderCode}."),
            TenNguoiNhan = e.ToName,
            DoanMo = [Theo(l, $"Bạn đã mua {soVe} vé cho buổi diễn dưới đây.", $"You bought {soVe} ticket(s) for the show below.")],
            Bang = bang,
            Nut = e.TicketUrl is null ? null : new NutThu(Theo(l, "Xem vé của tôi", "View my tickets"), e.TicketUrl),
            DoanSau =
            [
                e.Online
                    ? Theo(l, "Khi buổi diễn bắt đầu, vào trang Vé của tôi để xem trực tuyến.", "When the show starts, open My tickets to watch online.")
                    : Theo(l, "Mã QR vào cửa nằm trong trang Vé của tôi. Đưa mã cho nhân viên phòng trà quét khi tới nơi; nên tới trước giờ diễn khoảng 30 phút.",
                        "Your entry QR code is in My tickets. Show it to the venue staff when you arrive; please come about 30 minutes early."),
                Theo(l, "Không tham dự được? Xem điều kiện hoàn vé trong trang chi tiết vé.", "Can't make it? See the refund terms on the ticket page."),
            ],
            LyDoNhan = Theo(l, "Bạn nhận thư này vì đã mua vé trên MusicLounge bằng tài khoản dùng địa chỉ email này.",
                "You received this email because you bought tickets on MusicLounge with the account using this address."),
        };
        return GuiAsync(e.ToEmail, e.ToName, Theo(l, $"Vé của bạn: {e.ShowName}", $"Your tickets: {e.ShowName}"), thu, ct);
    }

    /// <summary>
    /// MLACP-635: dựng thư HAI phần (chữ trơn trước, HTML sau — RFC 2046: phần cuối là phần ưu tiên hiển thị) rồi gửi.
    /// Tách riêng <see cref="TaoThu"/> để test kiểm nội dung thư mà không cần máy chủ SMTP.
    /// </summary>
    private async Task GuiAsync(
        string toEmail, string toName, string subject, ThuEmail thu, CancellationToken ct, EmailAttachment? attachment = null)
    {
        using var message = TaoThu(_settings, toEmail, toName, subject, thu, attachment);
        if (BatThu is not null) { await BatThu(message); return; }
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };
        await client.SendMailAsync(message, ct);
    }

    /// <summary>
    /// Chỉ test đặt: nhận thư đã dựng xong thay cho việc gửi SMTP, để kiểm nội dung thật (hai phần, tiêu đề, ngôn ngữ) mà
    /// không cần máy chủ thư. Có giá trị thì coi như "đã cấu hình cách gửi".
    /// </summary>
    internal Func<MailMessage, Task>? BatThu { get; set; }

    private bool CoCachGui => !string.IsNullOrWhiteSpace(_settings.Host) || BatThu is not null;

    internal static MailMessage TaoThu(
        EmailSettings settings, string toEmail, string toName, string subject, ThuEmail thu, EmailAttachment? attachment = null)
    {
        var (html, chuTron) = KhungThu.Dung(thu);
        var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
        };
        message.To.Add(new MailAddress(toEmail, toName));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(chuTron, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html));
        // MLACP-673: MailMessage tự chuyển thành multipart/mixed (hai phần chữ + tệp) khi có đính kèm. Luồng nhớ được
        // MailMessage.Dispose giải phóng cùng thư.
        if (attachment is not null)
            message.Attachments.Add(new Attachment(new MemoryStream(attachment.Content), attachment.FileName, attachment.MimeType));
        return message;
    }

    private static string Theo(string language, string vi, string en) => new SongNgu(vi, en).Theo(language);

    private static readonly string[] ThuVi = ["Chủ nhật", "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy"];

    /// <summary>"Thứ sáu, 09/10/2026" theo GIỜ VIỆT NAM — suất 23:30 ngày 9 tính theo UTC đã là ngày 10.</summary>
    private static string ThuNgay(DateTimeOffset t, string language)
    {
        var vn = t.ToOffset(VietnamTime.Offset);
        return NgonNgu.LaTiengAnh(language)
            ? vn.ToString("dddd, dd/MM/yyyy", CultureInfo.InvariantCulture)
            // Qua VietnamTime.Format (InvariantCulture): "/" trong chuỗi định dạng là dấu phân cách ngày CỦA VÙNG đang chạy —
            // máy đặt vùng khác in "09-10-2026" (bản xem trước 05/10 đã in đúng như vậy).
            : $"{ThuVi[(int)vn.DayOfWeek]}, {VietnamTime.Format(t, "dd/MM/yyyy")}";
    }

    /// <summary>VND không có đơn vị lẻ: in số nguyên đồng.</summary>
    private static string Tien(decimal soTien, string language) => NgonNgu.LaTiengAnh(language)
        ? decimal.Round(soTien, 0).ToString("N0", CultureInfo.InvariantCulture) + " VND"
        : decimal.Round(soTien, 0).ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";

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
