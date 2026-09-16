using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-421. Cloudflare Workers AI (nhà cung cấp miễn phí thêm ở MLACP-418) trả ảnh **JPEG**, còn OpenAI trả PNG.
/// Chỗ lưu poster lại đặt tên cứng "poster.png", trong khi bộ kiểm tra file đối chiếu phần mở rộng với chữ ký thật
/// (chống đổi đuôi để lách kiểm duyệt) — nên ảnh JPEG bị từ chối ngay tại bước lưu, sau khi đã tiêu một lượt gọi
/// Workers AI. Phát hiện khi gọi thật Cloudflare ngày 16/09.
/// </summary>
[Collection("Integration")]
public sealed class PosterImageFormatTests
{
    private readonly ApiFactory _factory;

    public PosterImageFormatTests(ApiFactory factory) => _factory = factory;

    private sealed class NhaCungCapGia(byte[] anh) : IAiImageGenerationService
    {
        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default) => Task.FromResult(anh);
    }

    private static byte[] AnhJpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    private static byte[] AnhPng() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private WebApplicationFactory<Program> VoiNhaCungCap(byte[] anh) =>
        // Không dispose: Program.cs gọi Log.CloseAndFlush() khi host tắt, huỷ giữa phiên sẽ tắt luôn nhật ký của
        // các test khác (xem StorageRootPathTests).
        _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.Replace(ServiceDescriptor.Scoped<IAiImageGenerationService>(_ => new NhaCungCapGia(anh)))));

    private async Task<int> TaoShowAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"PosterFormat-{Guid.NewGuid():N}",
            Description = "test",
            Format = "Offline",
            ScheduledStart = SeedHelper.NextShowStart(),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>())!.Data;
    }

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    public async Task NhaCungCapTraAnh_LuuDuocVaGiuDungDinhDang(string dinhDang)
    {
        var factory = VoiNhaCungCap(dinhDang == "jpeg" ? AnhJpeg() : AnhPng());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserId, SeedHelper.OwnerId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRole, "Owner");
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderLoungeId, SeedHelper.LoungeId.ToString());
        var showId = await TaoShowAsync(client);

        var res = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            "ảnh hợp lệ phải lưu được, bất kể nhà cung cấp trả JPEG hay PNG");
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(dinhDang == "jpeg" ? ".jpg" : ".png",
            "tên file phải khớp định dạng thật, vì bộ kiểm tra đối chiếu chữ ký với phần mở rộng");
    }

    [Fact]
    public async Task NhaCungCapTraDuLieuKhongPhaiAnh_BaoLoiDichVu_KhongLuuRac()
    {
        var factory = VoiNhaCungCap("day khong phai anh"u8.ToArray());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserId, SeedHelper.OwnerId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRole, "Owner");
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderLoungeId, SeedHelper.LoungeId.ToString());
        var showId = await TaoShowAsync(client);

        var res = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private sealed record IdResponse(bool Success, int Data);
}
