using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-450. Chủ phòng trà trước đây không phân biệt được "bị từ chối duyệt" với "bản nháp chưa gửi" (cả hai đều
/// <c>Draft</c>), không đọc được lý do từ chối ngoài một thông báo, và không biết đã khai VCPMC chưa. Khối
/// <c>operatorInfo</c> trả những điều đó — chỉ cho người vận hành, vì endpoint chi tiết buổi hòa nhạc là công khai.
///
/// Các test đi đúng luồng thật: tạo buổi hòa nhạc → gửi duyệt → Admin duyệt/từ chối qua API → đọc chi tiết.
/// </summary>
[Collection("Integration")]
public sealed class OperatorShowInfoTests
{
    private readonly ApiFactory _factory;

    public OperatorShowInfoTests(ApiFactory factory) => _factory = factory;

    private HttpClient Chu() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<int> TaoBuoiHoaNhacAsync()
    {
        var res = await Chu().PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"OpInfo-{Guid.NewGuid():N}",
            Description = "Buổi hòa nhạc kiểm thử thông tin vận hành",
            Format = "Offline",
            ScheduledStart = SeedHelper.NextShowStart(),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = new[]
            {
                new { PerformerId = (int?)null, PerformerName = "Ca sĩ kiểm thử", Role = "Main", OrderIndex = 1, SetTime = (string?)null, AcceptsDonation = true }
            }
        });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var showId = doc.RootElement.GetProperty("data").GetInt32();

        (await Chu().PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-TEST-0450" })).EnsureSuccessStatusCode();

        (await Chu().PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId, Name = "Thường", Description = (string?)null, AccessType = "Physical",
            ZoneId = (int?)null, TotalCapacity = 100,
            Prices = new[]
            {
                new
                {
                    Name = "Giá thường", Price = 150_000m, Quota = (int?)50, PurchaseChannel = "Both",
                    SaleStart = DateTimeOffset.UtcNow, SaleEnd = DateTimeOffset.UtcNow.AddDays(2)
                }
            }
        })).EnsureSuccessStatusCode();

        return showId;
    }

    private async Task GuiDuyetAsync(int showId)
        => (await Chu().PostAsync($"/api/v1/lounge-shows/{showId}/submit", null)).EnsureSuccessStatusCode();

    private async Task DuyetAsync(int showId, string decision, string note)
        => (await Admin().PostAsJsonAsync($"/api/v1/moderations/shows/{showId}/review",
            new { Decision = decision, ReviewNote = note })).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DocChiTietAsync(HttpClient client, int showId)
    {
        var res = await client.GetAsync($"/api/v1/lounge-shows/{showId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task BiTuChoi_ChuThayQuyetDinhVaLyDo_DuStatusVeDraft()
    {
        var showId = await TaoBuoiHoaNhacAsync();
        await GuiDuyetAsync(showId);
        await DuyetAsync(showId, "Rejected", "Thiếu giấy phép biểu diễn của Sở VHTT");

        var data = await DocChiTietAsync(Chu(), showId);

        data.GetProperty("status").GetString().Should().Be("Draft", "tiền đề: bị từ chối thì quay về Draft");
        var mod = data.GetProperty("operatorInfo").GetProperty("moderation");
        mod.GetProperty("decision").GetString().Should().Be("Rejected");
        mod.GetProperty("reviewNote").GetString().Should().Be("Thiếu giấy phép biểu diễn của Sở VHTT");
        mod.GetProperty("reviewedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task DangChoDuyet_DecisionNull_CoHanSla()
    {
        var showId = await TaoBuoiHoaNhacAsync();
        await GuiDuyetAsync(showId);

        var mod = (await DocChiTietAsync(Chu(), showId)).GetProperty("operatorInfo").GetProperty("moderation");

        mod.GetProperty("decision").ValueKind.Should().Be(JsonValueKind.Null, "null nghĩa là đang chờ Admin");
        mod.GetProperty("slaDeadline").ValueKind.Should().NotBe(JsonValueKind.Null);
        mod.GetProperty("submittedAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5),
            "CreatedAt là DateTime UTC — đổi sai múi giờ sẽ lệch hàng giờ");
    }

    [Fact]
    public async Task ChuaTungGuiDuyet_ModerationNull()
    {
        var showId = await TaoBuoiHoaNhacAsync();

        var info = (await DocChiTietAsync(Chu(), showId)).GetProperty("operatorInfo");

        info.ValueKind.Should().Be(JsonValueKind.Object);
        info.GetProperty("moderation").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Vcpmc_ChuThayDaKhaiVaMa()
    {
        var showId = await TaoBuoiHoaNhacAsync();
        (await DocChiTietAsync(Chu(), showId)).GetProperty("operatorInfo").GetProperty("vcpmcDeclared").GetBoolean()
            .Should().BeFalse("tiền đề: chưa khai");

        (await Chu().PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/vcpmc-royalty",
            new { VcpmcRoyaltyReference = "VCPMC-2026-0450" })).EnsureSuccessStatusCode();

        var info = (await DocChiTietAsync(Chu(), showId)).GetProperty("operatorInfo");
        info.GetProperty("vcpmcDeclared").GetBoolean().Should().BeTrue();
        info.GetProperty("vcpmcRoyaltyReference").GetString().Should().Be("VCPMC-2026-0450");
    }

    /// <summary>Endpoint công khai: lý do từ chối, lịch sử duyệt và mã VCPMC không được lộ cho người ngoài.</summary>
    [Fact]
    public async Task NguoiNgoai_KhongThayOperatorInfo()
    {
        var showId = await TaoBuoiHoaNhacAsync();
        (await Chu().PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/vcpmc-royalty",
            new { VcpmcRoyaltyReference = "VCPMC-2026-BIMAT" })).EnsureSuccessStatusCode();
        await GuiDuyetAsync(showId);
        await DuyetAsync(showId, "Approved", "Đủ hồ sơ");

        var nguoiNgoai = new (string Ten, HttpClient Client)[]
        {
            ("khách vô danh", _factory.CreateClient()),
            ("khán giả đã đăng nhập", _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")),
            ("nhân viên phòng trà khác", _factory.CreateAuthenticatedClient(SeedHelper.OtherVenueStaffId, "Staff", SeedHelper.OtherLoungeId)),
        };

        foreach (var (ten, client) in nguoiNgoai)
        {
            var res = await client.GetAsync($"/api/v1/lounge-shows/{showId}");
            res.StatusCode.Should().Be(HttpStatusCode.OK, $"{ten}: buổi hòa nhạc đã duyệt là công khai");
            var raw = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(raw);
            doc.RootElement.GetProperty("data").GetProperty("operatorInfo").ValueKind
                .Should().Be(JsonValueKind.Null, $"{ten} không được thấy thông tin vận hành");
            raw.Should().NotContain("VCPMC-2026-BIMAT", $"{ten} không được thấy mã VCPMC ở bất kỳ đâu trong phản hồi");
        }

        // Chủ phòng trà vẫn thấy — và thấy đã được duyệt.
        (await DocChiTietAsync(Chu(), showId)).GetProperty("operatorInfo").GetProperty("moderation")
            .GetProperty("decision").GetString().Should().Be("Approved");
    }
}
