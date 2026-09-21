using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-480. Tạo ảnh nền poster bằng Gemini image API, sau khi chủ dự án bật thanh toán cho project chứa khoá
/// (21/09/2026 — trước đó cả bốn model ảnh đều trả <c>limit: 0</c> trên bậc miễn phí).
/// </summary>
public sealed class GeminiImageGenerationTests
{
    private const string AnhThat = "iVBORw0KGgo=";

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

    private static GeminiImageGenerationService TaoService(
        TraLoiSan handler, string key = "khoa-123", string model = "gemini-3.1-flash-image")
        => new(new MotClient(handler), Options.Create(new GeminiSettings { ApiKey = key, ImageModel = model }));

    /// <summary>Dựng lại đúng hình dạng phản hồi thật đã đo ngày 21/09/2026.</summary>
    private static string PhanHoi(string anhB64, string? chuKyThought = null) => JsonSerializer.Serialize(new
    {
        status = "completed",
        steps = new object[]
        {
            new { type = "thought", signature = chuKyThought ?? "c2hvcnQ=" },
            new { type = "model_output", content = new[] { new { type = "image", mime_type = "image/jpeg", data = anhB64 } } }
        }
    });

    [Fact]
    public async Task TaoAnhThanhCong_TraVeAnhVaGoiDungEndpointMoi()
    {
        var handler = new TraLoiSan(HttpStatusCode.OK, PhanHoi(AnhThat));

        var bytes = await TaoService(handler).GenerateImageAsync("Đêm nhạc Trịnh – Hạ trắng, ảnh nền không chữ");

        bytes.Should().Equal(Convert.FromBase64String(AnhThat));

        // Endpoint MỚI. Bản GeminiImageGenerationService cũ (xoá 8/2026) gọi generateContent — gọi nhầm đường đó thì
        // API trả 404 và thông báo không hề nhắc tới ảnh, rất khó lần ra.
        handler.YeuCauCuoi!.RequestUri!.ToString()
            .Should().Be("https://generativelanguage.googleapis.com/v1beta/interactions");
        handler.YeuCauCuoi.Headers.GetValues("x-goog-api-key").Single().Should().Be("khoa-123");
        handler.BodyCuoi.Should().Contain("gemini-3.1-flash-image").And.Contain("\"aspect_ratio\":\"3:4\"");
    }

    /// <summary>
    /// Bẫy thật, đo được 21/09/2026: phản hồi có HAI khối base64, và khối KHÔNG PHẢI ảnh lại LỚN HƠN — bước
    /// <c>thought</c> mang <c>signature</c> 1.064.956 ký tự, ảnh thật chỉ 643.948. Bộ đọc nào chọn "chuỗi base64 dài
    /// nhất" sẽ ghi ra tệp hỏng, và hỏng theo kiểu tệ nhất: có tệp, đúng phần mở rộng, mở ra mới biết.
    /// </summary>
    [Fact]
    public async Task ChuKyCuaBuocThought_DaiHonAnh_VanPhaiLayDungAnh()
    {
        var chuKyRatDai = Convert.ToBase64String(Enumerable.Repeat((byte)0x41, 2048).ToArray());
        chuKyRatDai.Length.Should().BeGreaterThan(AnhThat.Length, "phải dựng đúng thế khó thì phép kiểm mới có nghĩa");

        var handler = new TraLoiSan(HttpStatusCode.OK, PhanHoi(AnhThat, chuKyRatDai));

        var bytes = await TaoService(handler).GenerateImageAsync("ảnh nền");

        bytes.Should().Equal(Convert.FromBase64String(AnhThat),
            "phải lấy theo type=model_output/image, không phải theo độ dài chuỗi");
    }

    [Fact]
    public async Task PhanHoiKhongCoBuocModelOutput_BaoLoiRoRang()
    {
        var body = JsonSerializer.Serialize(new
        {
            status = "completed",
            steps = new object[] { new { type = "thought", signature = "c2hvcnQ=" } }
        });
        var handler = new TraLoiSan(HttpStatusCode.OK, body);

        var act = () => TaoService(handler).GenerateImageAsync("ảnh nền");

        (await act.Should().ThrowAsync<ExternalServiceException>()).Which.Message.Should().Contain("no image data");
    }

    [Fact]
    public async Task ThieuCauHinh_BaoLoiTruocKhiGoiMang()
    {
        var handler = new TraLoiSan(HttpStatusCode.OK, PhanHoi(AnhThat));

        var act = () => TaoService(handler, model: "").GenerateImageAsync("ảnh nền");

        await act.Should().ThrowAsync<ExternalServiceException>();
        handler.YeuCauCuoi.Should().BeNull("thiếu cấu hình thì không được gọi đi đâu cả");
    }

    [Fact]
    public async Task MayChuTraLoi429_GiuNguyenThongBaoDeNguoiVanHanhDoiChieu()
    {
        // Đây đúng là thông báo thật khi project chưa bật thanh toán. Giữ nguyên văn trong log là thứ giúp phân biệt
        // "chưa bật thanh toán" với "hết hạn mức" — hai việc phải xử lý khác nhau.
        const string loiThat = "{\"error\":{\"message\":\"Rate limit exceeded (limit: 0 requests per day on Free Tier).\"}}";
        var handler = new TraLoiSan(HttpStatusCode.TooManyRequests, loiThat);

        var act = () => TaoService(handler).GenerateImageAsync("ảnh nền");

        (await act.Should().ThrowAsync<ExternalServiceException>())
            .Which.Message.Should().Contain("429").And.Contain("Free Tier");
    }
}
