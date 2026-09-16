using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-418. Tạo poster AI trước đây chỉ gọi được OpenAI — nhà cung cấp KHÔNG có bậc miễn phí — nên trên môi trường
/// chưa nạp tiền, một tính năng nằm trong gói trả phí luôn hỏng. Cloudflare Workers AI (FLUX.1 schnell) có bậc miễn phí
/// ~230 ảnh/ngày và dự án đã có sẵn cấu hình Cloudflare.
/// </summary>
public sealed class CloudflareImageGenerationTests
{
    private const string AnhPngGia = "iVBORw0KGgo=";

    private sealed class TraLoiSan(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? YeuCauCuoi { get; private set; }
        public string? BodyCuoi { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            YeuCauCuoi = request;
            BodyCuoi = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class MotClient(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static CloudflareImageGenerationService TaoService(
        TraLoiSan handler, string accountId = "acc-123", string token = "tok-123", string model = "")
        => new(new MotClient(handler), Options.Create(new CloudflareSettings
        {
            AccountId = accountId, ApiToken = token, ImageModel = model
        }));

    [Fact]
    public async Task TaoAnhThanhCong_TraVeAnhVaGoiDungModelMienPhi()
    {
        var handler = new TraLoiSan(HttpStatusCode.OK, $"{{\"result\":{{\"image\":\"{AnhPngGia}\"}},\"success\":true}}");

        var bytes = await TaoService(handler).GenerateImageAsync("Đêm nhạc Trịnh – Hạ trắng, poster ấm cúng");

        bytes.Should().Equal(Convert.FromBase64String(AnhPngGia));
        handler.YeuCauCuoi!.RequestUri!.ToString().Should().Be(
            "https://api.cloudflare.com/client/v4/accounts/acc-123/ai/run/@cf/black-forest-labs/flux-1-schnell");
        handler.YeuCauCuoi.Headers.Authorization!.ToString().Should().Be("Bearer tok-123");
        handler.BodyCuoi.Should().Contain("\"steps\":4");
    }

    [Fact]
    public async Task PromptQuaDai_BiCatConGioiHanCuaCloudflare()
    {
        var handler = new TraLoiSan(HttpStatusCode.OK, $"{{\"result\":{{\"image\":\"{AnhPngGia}\"}},\"success\":true}}");

        await TaoService(handler).GenerateImageAsync(new string('a', 3000));

        // 2048 là trần Cloudflare công bố; gửi quá sẽ bị trả 400 và người dùng mất một lần tạo poster.
        handler.BodyCuoi.Should().Contain(new string('a', 2048));
        handler.BodyCuoi.Should().NotContain(new string('a', 2049));
    }

    [Fact]
    public async Task ChuaCauHinhCloudflare_BaoLoiDichVuNgoai_KhongGoiMang()
    {
        var handler = new TraLoiSan(HttpStatusCode.OK, "{}");

        var act = () => TaoService(handler, accountId: "", token: "").GenerateImageAsync("poster");

        await act.Should().ThrowAsync<ExternalServiceException>().WithMessage("*Cloudflare:AccountId*");
        handler.YeuCauCuoi.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"errors\":[{\"message\":\"hết hạn mức miễn phí hôm nay\"}]}")]
    [InlineData(HttpStatusCode.OK, "{\"result\":{},\"success\":true}")]
    [InlineData(HttpStatusCode.OK, "{\"result\":{\"image\":\"khong-phai-base64!!\"},\"success\":true}")]
    public async Task CloudflareTraLoiHong_BaoLoiDichVuNgoai(HttpStatusCode code, string body)
    {
        var act = () => TaoService(new TraLoiSan(code, body)).GenerateImageAsync("poster");

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Theory]
    [InlineData("", "", false)]
    [InlineData("acc", "", false)]
    [InlineData("", "tok", false)]
    [InlineData("acc", "tok", true)]
    public void ChonNhaCungCap_ChiDungCloudflareKhiDuCauHinh(string accountId, string token, bool mongDoi)
        => AiImageProvider.UseCloudflare(new CloudflareSettings { AccountId = accountId, ApiToken = token })
            .Should().Be(mongDoi);
}
