using MusicLounge.Domain.ValueObjects;
using System.Net;
using System.Text;
using System.Web;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// MLACP-426. Trước đây SmsService là bản giả: người dùng bấm "gửi mã xác minh" không bao giờ nhận được tin, và bản giả
/// còn ghi MÃ OTP DẠNG RÕ ra log — trong khi SendPhoneVerificationCodeJob được thiết kế riêng để mã chỉ tồn tại trong
/// bộ nhớ. Nhà cung cấp là Twilio (chủ đã chốt 18/08); hành vi bám theo tài liệu chính thức của Twilio: POST form-encoded
/// tới /2010-04-01/Accounts/{AccountSid}/Messages.json, Basic auth, chỉ thử lại với 429 và 5xx.
/// </summary>
public sealed class SmsServiceTests
{
    private const string Ma = "482915";

    private static SmsSettings DaCauHinh() => new()
    {
        AccountSid = "AC_test_account_sid",
        AuthToken = "secret-token",
        FromNumber = "+15551234567"
    };

    private sealed class TwilioGia : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage>? _traLoi;
        private readonly Exception? _loi;

        public TwilioGia(HttpStatusCode code, string body)
            => _traLoi = () => new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        public TwilioGia(Exception loi) => _loi = loi;

        public int SoLanGoi { get; private set; }
        public HttpRequestMessage? YeuCau { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            SoLanGoi++;
            YeuCau = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            if (_loi is not null) throw _loi;
            return _traLoi!();
        }
    }

    private sealed class MotClient(HttpMessageHandler handler) : IHttpClientFactory
    {
        public string? TenDaXin { get; private set; }

        public HttpClient CreateClient(string name)
        {
            TenDaXin = name;
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    /// <summary>Ghi lại cả câu log đã định dạng lẫn từng giá trị cấu trúc, để kiểm được mã OTP không lọt ra ở đâu.</summary>
    private sealed class GhiLog : ILogger<SmsService>
    {
        public List<(LogLevel Muc, string NoiDung)> Ban { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var noiDung = formatter(state, exception);
            if (state is IReadOnlyList<KeyValuePair<string, object?>> giaTri)
                noiDung += " | " + string.Join(", ", giaTri.Select(g => $"{g.Key}={g.Value}"));
            if (exception is not null) noiDung += " | " + exception;
            Ban.Add((logLevel, noiDung));
        }
    }

    private static (SmsService Service, MotClient Factory, GhiLog Log) Tao(HttpMessageHandler http, SmsSettings? cauHinh = null)
    {
        var factory = new MotClient(http);
        var log = new GhiLog();
        return (new SmsService(factory, Options.Create(cauHinh ?? DaCauHinh()), log), factory, log);
    }

    [Fact]
    public async Task ChuaCauHinh_KhongGoiTwilio_KhongNemLoi()
    {
        var http = new TwilioGia(HttpStatusCode.Created, "{}");
        var (service, _, log) = Tao(http, new SmsSettings());

        await service.SendPhoneVerificationCodeAsync("0912345678", Ma, NgonNgu.Viet);

        http.SoLanGoi.Should().Be(0);
        log.Ban.Should().Contain(b => b.Muc == LogLevel.Error && b.NoiDung.Contains("Sms:AccountSid"),
            "người dùng đang chờ một mã sẽ không tới — phải ghi rõ thiếu cấu hình nào");
    }

    [Fact]
    public async Task GuiThanhCong_DungDiaChiXacThucVaNoiDungTheoTaiLieuTwilio()
    {
        var http = new TwilioGia(HttpStatusCode.Created, "{\"sid\":\"SM0123\",\"status\":\"queued\"}");
        var (service, factory, _) = Tao(http);

        await service.SendPhoneVerificationCodeAsync("0912345678", Ma, NgonNgu.Viet);

        factory.TenDaXin.Should().Be(SmsService.HttpClientName);
        http.YeuCau!.Method.Should().Be(HttpMethod.Post);
        http.YeuCau.RequestUri!.ToString().Should().Be(
            "https://api.twilio.com/2010-04-01/Accounts/AC_test_account_sid/Messages.json");
        http.YeuCau.Headers.Authorization!.Scheme.Should().Be("Basic");
        Encoding.UTF8.GetString(Convert.FromBase64String(http.YeuCau.Headers.Authorization.Parameter!))
            .Should().Be("AC_test_account_sid:secret-token");
        http.YeuCau.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");

        var form = HttpUtility.ParseQueryString(http.Body!);
        form["To"].Should().Be("+84912345678", "Twilio yêu cầu số người nhận ở dạng E.164");
        form["From"].Should().Be("+15551234567");
        form["Body"].Should().Contain(Ma);
    }

    [Theory]
    [InlineData("0912345678", "+84912345678")]
    [InlineData("84912345678", "+84912345678")]
    [InlineData("+84 912 345 678", "+84912345678")]
    [InlineData("091-234-5678", "+84912345678")]
    [InlineData("+14155552671", "+14155552671")]
    public void ChuanHoaSoDienThoai_VeE164(string nhap, string mongDoi)
        => SmsService.ChuanHoaE164(nhap).Should().Be(mongDoi);

    [Theory]
    // Số nước ngoài gõ thiếu dấu "+": cách đoán cũ tự dán "+84" thành "+8412089464415" và gửi nhầm chỗ. Phải từ chối.
    [InlineData("12089464415")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void SoKhongHopLe_TraVeNull(string? nhap)
        => SmsService.ChuanHoaE164(nhap).Should().BeNull();

    [Fact]
    public async Task SoKhongHopLe_KhongGoiTwilio_KhongNemLoi()
    {
        var http = new TwilioGia(HttpStatusCode.Created, "{}");
        var (service, _, log) = Tao(http);

        await service.SendPhoneVerificationCodeAsync("12089464415", Ma, NgonNgu.Viet);

        http.SoLanGoi.Should().Be(0);
        log.Ban.Should().Contain(b => b.Muc == LogLevel.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task LoiTamThoi_NemLoiDeHangfireThuLai(HttpStatusCode code)
    {
        // Twilio: 429 là chạm giới hạn đồng thời, 5xx là sự cố tạm thời phía họ — nên thử lại.
        var (service, _, _) = Tao(new TwilioGia(code, "{\"code\":20429,\"message\":\"Too Many Requests\",\"status\":429}"));

        var act = () => service.SendPhoneVerificationCodeAsync("0912345678", Ma, NgonNgu.Viet);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task LoiMang_NemLoiDeHangfireThuLai()
    {
        var (service, _, _) = Tao(new TwilioGia(new HttpRequestException("Connection reset by peer")));

        var act = () => service.SendPhoneVerificationCodeAsync("0912345678", Ma, NgonNgu.Viet);

        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    // 21608: tài khoản trial chỉ gửi được tới số đã xác minh. 21612: không định tuyến được (số VN bằng số dài).
    [InlineData(HttpStatusCode.BadRequest, 21608)]
    [InlineData(HttpStatusCode.BadRequest, 21612)]
    [InlineData(HttpStatusCode.Unauthorized, 20003)]
    public async Task LoiVinhVien4xx_KhongNemLoi_GhiMaLoiTwilio(HttpStatusCode code, int maTwilio)
    {
        // Thử lại một lỗi vĩnh viễn chỉ tốn thêm lượt gọi mà không bao giờ thành công.
        var body = $"{{\"code\":{maTwilio},\"message\":\"rejected\",\"more_info\":\"https://www.twilio.com/docs/errors/{maTwilio}\",\"status\":{(int)code}}}";
        var (service, _, log) = Tao(new TwilioGia(code, body));

        await service.SendPhoneVerificationCodeAsync("0912345678", Ma, NgonNgu.Viet);

        log.Ban.Should().Contain(b => b.Muc == LogLevel.Error && b.NoiDung.Contains(maTwilio.ToString()),
            "mã lỗi Twilio là thứ duy nhất cho biết vì sao tin không tới");
    }

    [Theory]
    [InlineData("khong phai json")]
    [InlineData("{\"code\":\"abc\",\"message\":123,\"status\":\"bad\"}")]
    [InlineData("{}")]
    [InlineData("")]
    public async Task PhanHoiLoiKhongDungKhuon_KhongSap(string body)
    {
        // Twilio trả "status" là CHUỖI khi thành công ("queued") nhưng là SỐ khi lỗi (400) — đọc cứng theo một kiểu là
        // nổ ngay trong nhánh xử lý lỗi.
        var (service, _, _) = Tao(new TwilioGia(HttpStatusCode.BadRequest, body));

        var act = () => service.SendPhoneVerificationCodeAsync("0912345678", Ma, NgonNgu.Viet);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void NoiDungTinNhan_GonTrongMotDoanUcs2()
    {
        // Tiếng Việt có dấu không thuộc bảng GSM-7 nên tin chuyển sang UCS-2: tối đa 70 ký tự một đoạn, và Twilio tính
        // tiền theo TỪNG đoạn. Vượt 70 là mỗi mã OTP tốn gấp đôi.
        var noiDung = SmsService.NoiDung("999999");

        noiDung.Should().Contain("999999");
        noiDung.Length.Should().BeLessThanOrEqualTo(70);
    }

    [Fact]
    public async Task KhongNhanhNaoGhiMaOtpHaySoDayDuRaLog()
    {
        var log = new List<string>();
        async Task Chay(HttpMessageHandler http, SmsSettings? cauHinh = null, string so = "0912345678")
        {
            var (service, _, ghi) = Tao(http, cauHinh);
            try { await service.SendPhoneVerificationCodeAsync(so, Ma, NgonNgu.Viet); }
            // Loi tam thoi nem ra de Hangfire thu lai — va Hangfire GHI noi dung ngoai le vao log, nen soi ca no.
            catch (Exception ex) { log.Add(ex.ToString()); }
            log.AddRange(ghi.Ban.Select(b => b.NoiDung));
        }

        await Chay(new TwilioGia(HttpStatusCode.Created, "{}"), new SmsSettings());
        await Chay(new TwilioGia(HttpStatusCode.Created, "{}"), so: "12089464415");
        await Chay(new TwilioGia(HttpStatusCode.Created, "{\"sid\":\"SM1\",\"status\":\"queued\"}"));
        await Chay(new TwilioGia(HttpStatusCode.BadRequest, "{\"code\":21608,\"message\":\"x\",\"status\":400}"));
        // Thông báo lỗi thật của Twilio thường nhắc lại chính số người nhận — ghi nguyên văn ra log là lộ số.
        await Chay(new TwilioGia(HttpStatusCode.BadRequest,
            "{\"code\":21211,\"message\":\"The 'To' number +84912345678 is not a valid phone number.\",\"status\":400}"));
        await Chay(new TwilioGia(HttpStatusCode.ServiceUnavailable,
            "{\"code\":20503,\"message\":\"Service unavailable while sending to +84912345678\",\"status\":503}"));
        await Chay(new TwilioGia(HttpStatusCode.InternalServerError, "{}"));
        await Chay(new TwilioGia(new HttpRequestException("reset")));

        log.Should().NotBeEmpty();
        log.Should().NotContain(d => d.Contains(Ma), "mã OTP chỉ được tồn tại trong bộ nhớ, không nằm trong log");
        log.Should().NotContain(d => d.Contains("0912345678") || d.Contains("+84912345678"),
            "số điện thoại là dữ liệu cá nhân — cột trong DB đã được mã hoá, log không được lộ ra đủ số");
    }
}
