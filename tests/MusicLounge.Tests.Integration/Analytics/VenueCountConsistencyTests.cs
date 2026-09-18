using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-452. Production cho ba con số khác nhau cho cùng câu hỏi "có bao nhiêu phòng trà": /analytics/platform = 3,
/// /analytics/admin-overview = 2, danh sách công khai = 2 — không trường nào nói mình đếm gì. Nay "đang hoạt động" ở cả ba
/// nơi đều là <c>VenueLifecycle.Operating</c>, còn <c>TotalVenues</c> được giữ nguyên nghĩa (mọi trạng thái) và giải thích
/// bằng <c>VenuesByStatus</c>.
/// </summary>
[Collection("Integration")]
public sealed class VenueCountConsistencyTests
{
    private readonly ApiFactory _factory;

    public VenueCountConsistencyTests(ApiFactory factory) => _factory = factory;

    private static async Task<JsonElement> DataAsync(HttpResponseMessage res)
    {
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task SoPhongTraDangHoatDong_GiongNhauO_CaBaNoi()
    {
        // Một phòng trà đang chờ duyệt: được tính vào TotalVenues nhưng KHÔNG phải đang hoạt động — đúng tình huống
        // ở production (phòng trà "FPT Uni").
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = new MusicLounge.Domain.Entities.User { Email = $"vc-{Guid.NewGuid():N}@test.com", FullName = "Chờ duyệt" };
            db.Users.Add(owner);
            await db.SaveChangesAsync();
            db.Lounges.Add(new MusicLoungeVenue
            {
                OwnerId = owner.Id, Name = $"VC-{Guid.NewGuid():N}"[..20], Status = LoungeStatus.Pending,
                Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
            });
            await db.SaveChangesAsync();
        }

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var platform = await DataAsync(await admin.GetAsync("/api/v1/analytics/platform"));
        var overview = await DataAsync(await admin.GetAsync("/api/v1/analytics/admin-overview"));
        var congKhai = await DataAsync(await _factory.CreateClient().GetAsync("/api/v1/lounges?page=1&pageSize=1"));

        var operating = platform.GetProperty("operatingVenues").GetInt32();
        operating.Should().Be(overview.GetProperty("activeVenuesCount").GetInt32(),
            "/platform và /admin-overview phải cùng một định nghĩa 'đang hoạt động'");
        operating.Should().Be(congKhai.GetProperty("totalCount").GetInt32(),
            "danh sách công khai hiện đúng những phòng trà đang hoạt động");

        var total = platform.GetProperty("totalVenues").GetInt32();
        total.Should().BeGreaterThan(operating, "tiền đề: có phòng trà đang chờ duyệt, nên tổng phải lớn hơn số đang hoạt động");

        var theoTrangThai = platform.GetProperty("venuesByStatus").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetInt32());
        theoTrangThai.Keys.Should().BeEquivalentTo(Enum.GetNames<LoungeStatus>(),
            "đủ mọi khoá kể cả 0, để client không phải đoán khoá nào có mặt");
        theoTrangThai.Values.Sum().Should().Be(total, "bảng đếm theo trạng thái giải thích đúng TotalVenues");
        theoTrangThai["Pending"].Should().BeGreaterThan(0);
        (theoTrangThai["Approved"] + theoTrangThai["Warned"]).Should().Be(operating);
    }
}
