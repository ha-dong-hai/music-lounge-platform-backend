using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Infrastructure.Services;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-423. Tạo ảnh AI là lời gọi ra ngoài chậm nhất của hệ thống này — đo thật ngày 16/09 trên Cloudflare
/// Workers AI: flux-2-dev mất 77–86 giây một ảnh. Trước đây nhánh Cloudflare dùng chung HTTP client của livestream
/// (timeout 30 giây), nên mọi lần tạo poster bằng model FLUX 2 đều đứt giữa chừng: chủ phòng trà chờ 30 giây rồi
/// nhận lỗi, và đó là tính năng nằm trong gói subscription họ đã trả tiền.
/// </summary>
[Collection("Integration")]
public sealed class CauHinhHttpClientTests
{
    private readonly ApiFactory _factory;

    public CauHinhHttpClientTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void ClientTaoAnh_DuThoiGianChoModelChamNhat()
    {
        var httpFactory = _factory.Services.GetRequiredService<IHttpClientFactory>();

        var client = httpFactory.CreateClient(CloudflareImageGenerationService.HttpClientName);

        // So khớp đúng giá trị chứ không phải "lớn hơn 90 giây": một tên client CHƯA ĐĂNG KÝ vẫn trả về
        // HttpClient mặc định timeout 100 giây, nên phép so "lớn hơn 90" sẽ xanh ngay cả khi phần đăng ký
        // bị thiếu — test xanh mà tính năng vẫn hỏng.
        client.Timeout.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void ClientLivestream_VanGiuTimeoutNgan()
    {
        var httpFactory = _factory.Services.GetRequiredService<IHttpClientFactory>();

        // Livestream gọi Cloudflare Stream rất nhanh; nới timeout ở đây sẽ khiến một lời gọi treo giữ chân
        // thread của request khác, nên phải tách client chứ không nới chung.
        httpFactory.CreateClient("cloudflare").Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }
}
