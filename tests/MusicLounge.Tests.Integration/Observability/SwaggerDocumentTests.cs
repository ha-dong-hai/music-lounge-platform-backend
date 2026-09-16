using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace MusicLounge.Tests.Integration.Observability;

/// <summary>
/// MLACP-413. Tài liệu API trên Azure trả 500 suốt vì hai DTO khác namespace trùng tên
/// (<c>LoungeGalleryImageDto</c>): Swashbuckle đặt tên schema theo tên lớp nên bị đụng, và một lỗi như vậy chỉ lộ ra
/// khi có người mở trang tài liệu — không test nào chạm tới. Test này gọi thẳng tài liệu, nên lần sau thêm một DTO
/// trùng tên là đỏ ngay tại CI.
/// </summary>
[Collection("Integration")]
public sealed class SwaggerDocumentTests
{
    private readonly ApiFactory _factory;

    public SwaggerDocumentTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task TaiLieuApi_SinhDuocVaCoDuCacDuongDanChinh()
    {
        var res = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var paths = doc.RootElement.GetProperty("paths");
        paths.GetProperty("/api/v1/lounge-shows/{id}").ValueKind.Should().Be(JsonValueKind.Object);
        paths.GetProperty("/api/v1/tickets/walk-in").ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.GetProperty("components").GetProperty("schemas")
            .EnumerateObject().Should().NotBeEmpty();
    }
}
