using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// MLACP-431. Dịch vụ ghép ảnh nay bắt buộc header X-Stitcher-Key (trước đây không xác thực, ai biết địa chỉ cũng gọi được
/// và bắt nó tải URL bất kỳ). Backend phải gửi đúng khoá; thiếu khoá thì báo lỗi rõ ràng thay vì gọi đi rồi nhận 401.
/// </summary>
public sealed class PanoramaStitcherClientTests
{
    private sealed class DichVuGia : HttpMessageHandler
    {
        public HttpRequestMessage? YeuCau { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            YeuCau = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0xFF, 0xD8, 0xFF]) });
        }
    }

    private sealed class MotClient(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static HttpPanoramaStitchingService Tao(DichVuGia http, string apiKey) =>
        new(new MotClient(http), Options.Create(new PanoramaStitcherSettings
        {
            BaseUrl = "https://ghep-anh.test",
            PublicBaseUrl = "https://musiclounge-api.azurewebsites.net",
            ApiKey = apiKey
        }));

    [Fact]
    public async Task GuiKhoaXacThucTrongHeader()
    {
        var http = new DichVuGia();

        await Tao(http, "khoa-that").StitchAsync(["/uploads/a.jpg", "/uploads/b.jpg"]);

        http.YeuCau!.Headers.GetValues("X-Stitcher-Key").Should().Equal("khoa-that");
    }

    [Fact]
    public async Task ChuaCauHinhKhoa_BaoLoiRoRang_KhongGoiDichVu()
    {
        var http = new DichVuGia();

        var act = () => Tao(http, "").StitchAsync(["/uploads/a.jpg", "/uploads/b.jpg"]);

        await act.Should().ThrowAsync<ExternalServiceException>().WithMessage("*PanoramaStitcher:ApiKey*");
        http.YeuCau.Should().BeNull();
    }
}
