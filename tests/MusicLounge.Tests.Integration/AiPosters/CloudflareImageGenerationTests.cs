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
        public string? TenClientDaXin { get; private set; }

        public HttpClient CreateClient(string name)
        {
            TenClientDaXin = name;
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private static CloudflareImageGenerationService TaoService(
        TraLoiSan handler, string accountId = "acc-123", string token = "tok-123", string model = "")
        => TaoServiceVoiFactory(handler, accountId, token, model).Service;

    private static (CloudflareImageGenerationService Service, MotClient Factory) TaoServiceVoiFactory(
        TraLoiSan handler, string accountId = "acc-123", string token = "tok-123", string model = "")
    {
        var factory = new MotClient(handler);
        return (new CloudflareImageGenerationService(factory, Options.Create(new CloudflareSettings
        {
            AccountId = accountId, ApiToken = token, ImageModel = model
        })), factory);
    }

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

    // ---- MLACP-423 ----

    [Theory]
    [InlineData("@cf/black-forest-labs/flux-2-klein-4b")]
    [InlineData("@cf/black-forest-labs/flux-2-klein-9b")]
    [InlineData("@cf/black-forest-labs/flux-2-dev")]
    public async Task ModelFlux2_PhaiGuiDangMultipart(string model)
    {
        // Cac model FLUX 2 chi nhan multipart/form-data. Goi that API Cloudflare ngay 16/09: cung mot prompt, gui JSON
        // bi tra "400 required properties at '/' are 'multipart'", gui multipart tra ve anh 690-770 KB. Neu khong tach
        // nhanh nay thi doi sang FLUX 2 la tinh nang hong ngay tu lan goi dau — chu phong tra mat luot tao poster.
        var handler = new TraLoiSan(HttpStatusCode.OK, $"{{\"result\":{{\"image\":\"{AnhPngGia}\"}},\"success\":true}}");

        await TaoService(handler, model: model).GenerateImageAsync("Đêm nhạc Trịnh – Hạ trắng");

        handler.YeuCauCuoi!.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
        // Nhay kep quanh ten bien: day la dang da goi thu that va Cloudflare chap nhan. MultipartFormDataContent
        // mac dinh ghi name=prompt khong nhay — chua kiem chung, nen ghim lai dang da biet chac chan chay duoc.
        handler.BodyCuoi.Should().Contain("Content-Disposition: form-data; name=\"prompt\"")
            .And.Contain("Đêm nhạc Trịnh – Hạ trắng");
        // Chi gui dung nhung gi da goi thu that va duoc chap nhan. Chua kiem chung FLUX 2 co nhan "steps" hay khong,
        // nen khong gui thua — mot lan bi tu choi la chu phong tra mat mot luot tao poster.
        handler.BodyCuoi.Should().NotContain("steps");
        // Ban goi thu that khong kem Content-Type o tung phan; .NET mac dinh tu them "text/plain; charset=utf-8".
        handler.BodyCuoi.Should().NotContain("text/plain");
    }

    [Fact]
    public async Task ModelFlux1Schnell_VanGuiJsonNhuCu()
    {
        // Doi chung cho test tren: sua cho FLUX 2 khong duoc lam hong nhanh dang chay tren Azure.
        var handler = new TraLoiSan(HttpStatusCode.OK, $"{{\"result\":{{\"image\":\"{AnhPngGia}\"}},\"success\":true}}");

        await TaoService(handler, model: "@cf/black-forest-labs/flux-1-schnell").GenerateImageAsync("poster");

        handler.YeuCauCuoi!.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
        handler.BodyCuoi.Should().Contain("\"steps\":4");
    }

    [Fact]
    public async Task TaoAnh_DungHttpClientRieng_KhongDungChungVoiLivestream()
    {
        // Client "cloudflare" la cua livestream, timeout 30 giay. Do that ngay 16/09: flux-2-dev mat 77-86 giay/anh,
        // nen dung chung client do thi moi lan tao poster deu dut giua chung. Timeout cua client rieng nay duoc
        // kiem o CauHinhHttpClientTests.
        var handler = new TraLoiSan(HttpStatusCode.OK, $"{{\"result\":{{\"image\":\"{AnhPngGia}\"}},\"success\":true}}");
        var (service, factory) = TaoServiceVoiFactory(handler);

        await service.GenerateImageAsync("poster");

        factory.TenClientDaXin.Should().Be(CloudflareImageGenerationService.HttpClientName);
        CloudflareImageGenerationService.HttpClientName.Should().NotBe("cloudflare");
    }
}
