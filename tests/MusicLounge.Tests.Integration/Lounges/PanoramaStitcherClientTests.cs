using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// Client goi dich vu ghep anh (services/panorama-stitcher).
///
/// MLACP-431: dich vu bat buoc header X-Stitcher-Key. Backend phai gui dung khoa; thieu khoa thi bao loi ro rang thay vi
/// goi di roi nhan 401.
///
/// MLACP-432:
///   - Dich vu tu tat khi khong dung (scale to zero), nen truoc khi ghep phai goi GET /health de danh thuc no.
///   - Chu phong tra chi thay loi do chinh bo anh cua ho (422). Moi loi he thong khac hien mot cau de hieu, con chi tiet
///     ky thuat ghi vao log.
/// </summary>
public sealed class PanoramaStitcherClientTests
{
    private static readonly string[] Anh = ["/uploads/a.jpg", "/uploads/b.jpg"];

    /// <summary>Tra loi theo duong dan va ghi lai moi yeu cau (kem header, doc truoc khi request bi huy).</summary>
    private sealed class DichVuGia : HttpMessageHandler
    {
        public Queue<HttpStatusCode> HealthTraVe { get; } = new();
        public bool ChanSauKhiHetCauTraLoi { get; set; }
        public Func<HttpResponseMessage> StitchTraVe { get; set; } =
            () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0xFF, 0xD8, 0xFF]) };
        public List<(HttpMethod Method, string Path, string? Khoa)> YeuCau { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var khoa = request.Headers.TryGetValues("X-Stitcher-Key", out var v) ? v.Single() : null;
            lock (YeuCau) YeuCau.Add((request.Method, request.RequestUri!.AbsolutePath, khoa));

            if (request.RequestUri.AbsolutePath == "/health")
            {
                if (HealthTraVe.Count > 0)
                    return Task.FromResult(new HttpResponseMessage(HealthTraVe.Dequeue()));

                // Hết câu trả lời dựng sẵn thì treo cho tới khi bị huỷ — dùng để ép lần gọi CUỐI bị chính
                // hạn chờ của client huỷ giữa chừng, một cách tất định thay vì trông vào lúc máy bận.
                if (ChanSauKhiHetCauTraLoi)
                    return Task.Run(async () =>
                    {
                        await Task.Delay(Timeout.Infinite, ct);
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    }, ct);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
            return Task.FromResult(StitchTraVe());
        }
    }

    private sealed class MotClient(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class GhiLog : ILogger<HttpPanoramaStitchingService>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static PanoramaStitcherSettings CauHinh(string apiKey = "khoa-that", string baseUrl = "https://ghep-anh.test",
        string publicBaseUrl = "https://musiclounge-api.azurewebsites.net") => new()
    {
        BaseUrl = baseUrl, PublicBaseUrl = publicBaseUrl, ApiKey = apiKey
    };

    private static HttpPanoramaStitchingService Tao(
        DichVuGia http, GhiLog log, PanoramaStitcherSettings? cauHinh = null, TimeSpan? choKhoiDong = null) =>
        new(new MotClient(http), Options.Create(cauHinh ?? CauHinh()), log,
            choKhoiDong ?? TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(10));

    private static HttpResponseMessage Loi(HttpStatusCode ma, string json) =>
        new(ma) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GuiKhoaXacThucTrongHeader()
    {
        var http = new DichVuGia();

        await Tao(http, new GhiLog()).StitchAsync(Anh);

        http.YeuCau.Single(r => r.Path == "/stitch").Khoa.Should().Be("khoa-that");
    }

    [Fact]
    public async Task DanhThucDichVuBangHealthTruocKhiGhep()
    {
        var http = new DichVuGia();

        await Tao(http, new GhiLog()).StitchAsync(Anh);

        http.YeuCau.Select(r => (r.Method, r.Path)).Should().Equal(
            (HttpMethod.Get, "/health"), (HttpMethod.Post, "/stitch"));
        http.YeuCau[0].Khoa.Should().BeNull("/health mở, không cần gửi khoá bí mật đi đâu thừa");
    }

    [Fact]
    public async Task DichVuDangKhoiDong_ThuLaiHealthToiKhiSanSangRoiMoiGhep()
    {
        // Luc container dang khoi dong, ingress cua Container Apps co the tra 502/503 thay vi giu yeu cau.
        var http = new DichVuGia();
        http.HealthTraVe.Enqueue(HttpStatusCode.ServiceUnavailable);
        http.HealthTraVe.Enqueue(HttpStatusCode.BadGateway);

        var anh = await Tao(http, new GhiLog()).StitchAsync(Anh);

        anh.Should().NotBeEmpty();
        http.YeuCau.Count(r => r.Path == "/health").Should().Be(3);
        http.YeuCau.Last().Path.Should().Be("/stitch");
    }

    [Fact]
    public async Task DichVuKhongDayNoi_BaoCauDeHieu_KhongGoiGhep()
    {
        var http = new DichVuGia();
        for (var i = 0; i < 10_000; i++) http.HealthTraVe.Enqueue(HttpStatusCode.ServiceUnavailable);
        var log = new GhiLog();

        var act = () => Tao(http, log, choKhoiDong: TimeSpan.FromMilliseconds(200)).StitchAsync(Anh);

        (await act.Should().ThrowAsync<ExternalServiceException>()).Which.Detail
            .Should().Be(HttpPanoramaStitchingService.ThongBaoSuCo);
        http.YeuCau.Should().NotContain(r => r.Path == "/stitch");
        log.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Text.Contains("/health") && e.Text.Contains("503"),
            "người vận hành cần biết dịch vụ không dậy và mã lỗi cuối cùng");
    }

    /// <summary>
    /// Lần gọi /health CUỐI gần như luôn bị chính hạn chờ của mình huỷ giữa chừng. Nếu để thông báo huỷ
    /// đó ghi đè lên mã trạng thái thật, người vận hành đọc log chỉ thấy "A task was canceled" và không
    /// biết dịch vụ đang tắt hẳn hay đang bật mà trả 503 — hai tình huống cần hai cách xử lý khác nhau.
    ///
    /// <para>Đây cũng chính là chỗ làm phép kiểm bên dưới CHẬP CHỜN (đỏ 1/3 lần chạy): nó phụ thuộc vào
    /// việc lần gọi cuối có kịp trả lời trước khi hạn chờ đóng hay không. Sửa ở gốc thì phép kiểm hết
    /// chập chờn như một hệ quả, chứ không phải bằng cách nới thời gian cho đỡ đỏ. (MLACP-476)</para>
    /// </summary>
    [Fact]
    public async Task LanGoiCuoiBiHuy_VanGiuMaTrangThaiThatTrongLog()
    {
        var http = new DichVuGia { ChanSauKhiHetCauTraLoi = true };
        http.HealthTraVe.Enqueue(HttpStatusCode.ServiceUnavailable);   // đúng MỘT câu trả lời thật
        var log = new GhiLog();

        var act = () => Tao(http, log, choKhoiDong: TimeSpan.FromMilliseconds(300)).StitchAsync(Anh);

        await act.Should().ThrowAsync<ExternalServiceException>();

        var loi = log.Entries.Single(e => e.Level == LogLevel.Error).Text;
        loi.Should().Contain("503", "mã dịch vụ thật sự trả về là thứ người vận hành cần, đừng để lỗi huỷ của chính mình xoá mất");
        loi.Should().NotContain("canceled");
        loi.Should().NotContain("hủy");
    }

    [Fact]
    public async Task LoiDoChinhBoAnh422_GiuNguyenLoiHuongDanChoChuPhongTra()
    {
        const string lyDo = "Ảnh #2 không tìm đủ điểm trùng khớp với các ảnh còn lại — hãy chụp gối lên ảnh bên cạnh nhiều hơn.";
        var http = new DichVuGia
        {
            StitchTraVe = () => Loi(HttpStatusCode.UnprocessableEntity, $"{{\"detail\": \"{lyDo}\"}}")
        };

        var act = () => Tao(http, new GhiLog()).StitchAsync(Anh);

        // MLACP-435: DomainException — loi cua dau vao, tinh vao gioi han so lan ghep (CPU da chay).
        (await act.Should().ThrowAsync<DomainException>()).Which.Message.Should().Be(lyDo);
    }

    /// <summary>
    /// MLACP-435. Ghep qua thoi gian cho cua HttpClient nghia la CPU da chay suot thoi gian do. Neu coi la loi he thong
    /// (khong tinh vao gioi han), gui lien tuc bo anh nang la dot CPU vo han ma khong bao gio cham gioi han.
    /// </summary>
    [Fact]
    public async Task GhepQuaThoiGianCho_LaLoiTinhVaoGioiHan_KhongPhaiLoiHeThong()
    {
        var http = new DichVuGia { StitchTraVe = () => throw new TaskCanceledException("HttpClient.Timeout") };

        var act = () => Tao(http, new GhiLog()).StitchAsync(Anh);

        (await act.Should().ThrowAsync<DomainException>()).Which.Message
            .Should().Be(HttpPanoramaStitchingService.ThongBaoQuaLau);
    }

    [Fact]
    public async Task Loi422DoBodySaiDinhDang_KhongDuaMangLoiKyThuatChoChuPhongTra()
    {
        // FastAPI cung tra 422 khi body sai schema, nhung detail la MANG loi ky thuat — khong phai loi cua bo anh.
        var http = new DichVuGia
        {
            StitchTraVe = () => Loi(HttpStatusCode.UnprocessableEntity,
                "{\"detail\": [{\"loc\": [\"body\", \"image_urls\"], \"msg\": \"field required\"}]}")
        };

        var act = () => Tao(http, new GhiLog()).StitchAsync(Anh);

        (await act.Should().ThrowAsync<ExternalServiceException>()).Which.Detail
            .Should().Be(HttpPanoramaStitchingService.ThongBaoSuCo);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Thiếu hoặc sai khoá xác thực.")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Dịch vụ ghép ảnh chưa được cấu hình nguồn ảnh cho phép.")]
    [InlineData(HttpStatusCode.BadRequest, "Có ảnh không đến từ nguồn được phép.")]
    [InlineData(HttpStatusCode.InternalServerError, "Ghép ảnh thành công nhưng không mã hóa được ảnh kết quả.")]
    public async Task LoiPhiaHeThong_ChuPhongTraThayCauDeHieu_ChiTietVaoLog(HttpStatusCode ma, string chiTiet)
    {
        var http = new DichVuGia { StitchTraVe = () => Loi(ma, $"{{\"detail\": \"{chiTiet}\"}}") };
        var log = new GhiLog();

        var act = () => Tao(http, log).StitchAsync(Anh);

        (await act.Should().ThrowAsync<ExternalServiceException>()).Which.Detail
            .Should().Be(HttpPanoramaStitchingService.ThongBaoSuCo);
        log.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Text.Contains(((int)ma).ToString())
                                          && e.Text.Contains(chiTiet));
    }

    [Theory]
    [InlineData("", "https://ghep-anh.test", "https://musiclounge-api.azurewebsites.net", "PanoramaStitcher:ApiKey")]
    [InlineData("khoa-that", "", "https://musiclounge-api.azurewebsites.net", "PanoramaStitcher:BaseUrl")]
    [InlineData("khoa-that", "https://ghep-anh.test", "", "PanoramaStitcher:PublicBaseUrl")]
    public async Task ThieuCauHinh_KhongGoiDichVu_ChuPhongTraThayCauDeHieu_TenCauHinhVaoLog(
        string apiKey, string baseUrl, string publicBaseUrl, string tenCauHinh)
    {
        var http = new DichVuGia();
        var log = new GhiLog();
        var dichVu = Tao(http, log, CauHinh(apiKey, baseUrl, publicBaseUrl));

        dichVu.IsConfiguredFor(Anh).Should().BeFalse();
        var act = () => dichVu.StitchAsync(Anh);

        (await act.Should().ThrowAsync<ExternalServiceException>()).Which.Detail
            .Should().Be(HttpPanoramaStitchingService.ThongBaoSuCo);
        http.YeuCau.Should().BeEmpty();
        log.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Text.Contains(tenCauHinh));
    }

    [Fact]
    public void AnhTrenKhoDamMay_KhongCanPublicBaseUrl()
    {
        var dichVu = Tao(new DichVuGia(), new GhiLog(), CauHinh(publicBaseUrl: ""));

        dichVu.IsConfiguredFor(["https://firebasestorage.googleapis.com/a.jpg", "https://firebasestorage.googleapis.com/b.jpg"])
            .Should().BeTrue();
    }
}
