using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;
using PhoneNumbers;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-426: gui SMS xac minh qua Twilio Programmable Messaging.
///
/// Truoc day day la ban gia chi ghi log: nguoi dung bam "gui ma" khong bao gio nhan duoc tin, va ban gia con ghi MA OTP
/// DANG RO ra log — trong khi SendPhoneVerificationCodeJob duoc thiet ke rieng de ma chi ton tai trong bo nho.
///
/// Nha cung cap la Twilio vi chu da chot ngay 18/08 (demo gui tu so nuoc ngoai toi so nuoc ngoai). Twilio KHONG gui duoc
/// toi so Viet Nam bang so dai neu chua dang ky Alphanumeric Sender ID — do la rao chan nha mang, khong sua bang code.
///
/// Moi hanh vi duoi day bam theo tai lieu chinh thuc cua Twilio:
///   - POST /2010-04-01/Accounts/{AccountSid}/Messages.json, form-encoded, HTTP Basic AccountSid:AuthToken.
///   - Chi thu lai voi 429 (cham gioi han dong thoi) va 5xx (su co tam thoi): nem loi de Hangfire thu lai. Loi 4xx la
///     vinh vien (so chua xac minh o tai khoan trial, khong dinh tuyen duoc...), thu lai chi ton luot goi.
/// </summary>
internal sealed class SmsService : ISmsService
{
    public const string HttpClientName = "twilio";

    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    private readonly IHttpClientFactory _httpFactory;
    private readonly SmsSettings _settings;
    private readonly ILogger<SmsService> _logger;

    public SmsService(IHttpClientFactory httpFactory, IOptions<SmsSettings> settings, ILogger<SmsService> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendPhoneVerificationCodeAsync(string toPhone, string code, CancellationToken ct = default)
    {
        // Khong bao gio dua ma OTP hay du so dien thoai vao log: so dien thoai la du lieu ca nhan va cot trong DB da
        // duoc ma hoa, con log tren Azure thi ai co quyen xem site deu doc duoc.
        var soDaChe = CheSo(toPhone);

        if (string.IsNullOrWhiteSpace(_settings.AccountSid)
            || string.IsNullOrWhiteSpace(_settings.AuthToken)
            || string.IsNullOrWhiteSpace(_settings.FromNumber))
        {
            // Error chu khong phai Warning: nguoi dung dang ngoi cho mot ma se khong bao gio toi.
            _logger.LogError(
                "SMS NOT SENT — chưa cấu hình Twilio (Sms:AccountSid / Sms:AuthToken / Sms:FromNumber), nên mã xác minh "
                + "không tới được người dùng. Phone={Phone}",
                soDaChe);
            return;
        }

        var nguoiNhan = ChuanHoaE164(toPhone);
        if (nguoiNhan is null)
        {
            _logger.LogError(
                "SMS NOT SENT — số điện thoại không hợp lệ, không gửi để tránh gửi nhầm người. Phone={Phone}", soDaChe);
            return;
        }

        var http = _httpFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(_settings.AccountSid)}/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.AccountSid}:{_settings.AuthToken}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = nguoiNhan,
            ["From"] = _settings.FromNumber,
            ["Body"] = NoiDung(code)
        });

        // Loi mang / het thoi gian cho: de ngoai le di tiep len Hangfire de thu lai.
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "Đã gửi SMS xác minh tới {Phone}. Twilio sid {Sid}", soDaChe, DocTruong(body, "sid") ?? "(không rõ)");
            return;
        }

        var (maTwilio, thongBao) = DocLoi(body);
        // Thong bao loi cua Twilio thuong nhac lai chinh so nguoi nhan ("The 'To' number +84... is not valid").
        thongBao = LamSach(thongBao, nguoiNhan, toPhone, code);

        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            throw new ExternalServiceException(
                "Twilio",
                $"Gửi SMS xác minh tới {soDaChe} lỗi tạm thời: HTTP {(int)response.StatusCode}, mã Twilio {maTwilio} — "
                + $"{thongBao}. Hangfire sẽ thử lại.");

        _logger.LogError(
            "Twilio từ chối gửi SMS xác minh tới {Phone}: HTTP {Status}, mã Twilio {TwilioCode} — {TwilioMessage}. "
            + "Lỗi vĩnh viễn nên không thử lại. Tra mã lỗi tại https://www.twilio.com/docs/errors/{TwilioCode}",
            soDaChe, (int)response.StatusCode, maTwilio, thongBao, maTwilio);
    }

    /// <summary>
    /// Chuan hoa ve E.164 bang libphonenumber-csharp (ban C# chinh thuc cua thu vien Google). Mac dinh vung VN cho so
    /// go kieu noi dia ("0912..."), va THUC SU kiem tinh hop le thay vi doan: cach cu tu dan "+84" vao moi day so
    /// khong bat dau bang "+", "84" hay "0", nen so nuoc ngoai go thieu "+" ("12089464415") thanh "+8412089464415" va
    /// gui nham cho. Tra ve null khi khong hop le.
    /// </summary>
    internal static string? ChuanHoaE164(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        try
        {
            var so = PhoneUtil.Parse(phone, "VN");
            return PhoneUtil.IsValidNumber(so) ? PhoneUtil.Format(so, PhoneNumberFormat.E164) : null;
        }
        catch (NumberParseException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tieng Viet co dau khong thuoc bang GSM-7 nen tin chuyen sang UCS-2: toi da 70 ky tu mot doan, va Twilio tinh tien
    /// theo TUNG doan. Giu gon trong 70 ky tu. Khong ghi so phut het han vao day: thoi han nam o handler, ghi cung o day
    /// thi doi mot cho la tin nhan noi sai.
    /// </summary>
    internal static string NoiDung(string code) => $"Mã xác minh MusicLounge của bạn: {code}. Không chia sẻ mã này.";

    /// <summary>Chi giu 3 so cuoi — du de doi chieu khi ho tro nguoi dung, khong du de lo so.</summary>
    internal static string CheSo(string? phone)
    {
        var chuSo = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        return chuSo.Length <= 3 ? "***" : "***" + chuSo[^3..];
    }

    /// <summary>
    /// Doc JSON loi phong thu. Twilio tra "status" la CHUOI khi thanh cong ("queued") nhung la SO khi loi (400), va
    /// "code"/"more_info" khong phai luc nao cung co — doc cung theo mot kieu la no ngay trong nhanh xu ly loi.
    /// </summary>
    private static (string Ma, string ThongBao) DocLoi(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return ("?", CatNgan(body));
            return (DocGiaTri(doc.RootElement, "code") ?? "?",
                DocGiaTri(doc.RootElement, "message") ?? "(không có thông báo)");
        }
        catch (JsonException)
        {
            return ("?", CatNgan(body));
        }
    }

    private static string? DocTruong(string body, string ten)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? DocGiaTri(doc.RootElement, ten) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? DocGiaTri(JsonElement obj, string ten)
        => !obj.TryGetProperty(ten, out var v)
            ? null
            : v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => v.GetRawText()
            };

    private static string CatNgan(string text)
        => string.IsNullOrWhiteSpace(text) ? "(phản hồi rỗng)" : text.Length <= 200 ? text : text[..200] + "…";

    private static string LamSach(string text, params string?[] canChe)
    {
        foreach (var chuoi in canChe.Where(c => !string.IsNullOrWhiteSpace(c)).OrderByDescending(c => c!.Length))
            text = text.Replace(chuoi!, "***", StringComparison.Ordinal);
        return text;
    }
}
