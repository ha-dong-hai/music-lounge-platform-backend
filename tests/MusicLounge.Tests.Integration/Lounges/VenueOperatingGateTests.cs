using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// MLACP-354. <c>VenueLifecycle.CanOperate</c> ghi rõ nó quyết <i>"bán vé, nhận donation"</i> — nhưng
/// trước task này chỉ cổng nộp duyệt buổi diễn và các danh sách công khai dùng tới nó. Phòng trà đang
/// bị tạm đình chỉ hay khoá vĩnh viễn bị ẩn khỏi danh sách, nhưng ai có đường dẫn tới buổi diễn vẫn giữ
/// chỗ, trả tiền thật và donate được.
///
/// <para>Mỗi bài từ chối kiểm <b>đúng câu thông báo</b> của chốt mới, không chỉ mã 422 — không thì bài
/// có thể xanh vì một chốt khác đã chặn.</para>
/// </summary>
[Collection("Integration")]
public sealed class VenueOperatingGateTests
{
    private const string BuyerMessage = "tạm ngừng giao dịch trên nền tảng";

    private readonly ApiFactory _factory;

    public VenueOperatingGateTests(ApiFactory factory) => _factory = factory;

    private sealed record DataResponse<T>(bool Success, T Data);
    private sealed record HoldResult(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record Venue(int LoungeId, int ShowId, int OnlinePriceId, int CounterPriceId, int PerformanceId, int OwnerId);

    /// <summary>
    /// Phòng trà riêng của chủ phòng trà trong seed (gói 1000 vé/buổi — hạn mức không cản), để đổi trạng
    /// thái mà không ảnh hưởng phòng trà dùng chung.
    /// </summary>
    private async Task<Venue> CreateVenueAsync(LoungeShowStatus showStatus = LoungeShowStatus.Published)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = freshOwner.Id,
            Name = $"Gate {Guid.NewGuid():N}"[..14],
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var start = showStatus == LoungeShowStatus.Ongoing
            ? DateTimeOffset.UtcNow.AddHours(-1)
            : DateTimeOffset.UtcNow.AddDays(5);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id,
            Name = $"Show {Guid.NewGuid():N}"[..14],
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = showStatus,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(3),
            ActualStart = showStatus == LoungeShowStatus.Ongoing ? start : null
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier { LoungeShowId = show.Id, Name = "Standard", AccessType = AccessType.Physical };
        db.Add(tier);
        await db.SaveChangesAsync();

        TicketPrice NewPrice(PurchaseChannel channel) => new()
        {
            TierId = tier.Id, Name = channel.ToString(), Price = 100_000m, Quota = 100, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(4),
            PurchaseChannel = channel
        };
        var online = NewPrice(PurchaseChannel.Online);
        var counter = NewPrice(PurchaseChannel.Offline);
        db.AddRange(online, counter);

        var performance = new Performance
        {
            LoungeShowId = show.Id, PerformerId = SeedHelper.PerformerId, Role = PerformerRole.Main,
            AcceptsDonation = true
        };
        db.Add(performance);
        await db.SaveChangesAsync();

        return new Venue(lounge.Id, show.Id, online.Id, counter.Id, performance.Id, freshOwner.Id);
    }

    private async Task SetVenueStatusAsync(int loungeId, LoungeStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = await db.Lounges.SingleAsync(l => l.Id == loungeId);
        lounge.Status = status;
        await db.SaveChangesAsync();
    }

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

    private Task<HttpResponseMessage> HoldAsync(int priceId)
        => Audience().PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = priceId, Quantity = 1 });

    // ── Giữ chỗ ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LoungeStatus.Suspended)]
    [InlineData(LoungeStatus.Locked)]
    [InlineData(LoungeStatus.Pending)]
    [InlineData(LoungeStatus.Rejected)]
    public async Task PhongTraKhongDuocHoatDongThiKhongGiuChoDuoc(LoungeStatus status)
    {
        var venue = await CreateVenueAsync();
        await SetVenueStatusAsync(venue.LoungeId, status);

        var res = await HoldAsync(venue.OnlinePriceId);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "phòng trà bị ẩn khỏi danh sách nhưng ai có đường dẫn vẫn giữ chỗ được");
        (await res.Content.ReadAsStringAsync()).Should().Contain(BuyerMessage);
    }

    [Fact]
    public async Task PhongTraBiCanhCaoVanBanVeBinhThuong()
    {
        // Cảnh cáo là một vết ghi lại, không phải lệnh dừng — VenueLifecycle.Operating gồm Warned.
        var venue = await CreateVenueAsync();
        await SetVenueStatusAsync(venue.LoungeId, LoungeStatus.Warned);

        (await HoldAsync(venue.OnlinePriceId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── Trả tiền cho chỗ đã giữ trước khi bị đình chỉ ───────────────────────

    [Fact]
    public async Task GiuChoTruocKhiBiDinhChiThiKhongTraTienDuoc()
    {
        var venue = await CreateVenueAsync();
        var hold = await HoldAsync(venue.OnlinePriceId);
        hold.StatusCode.Should().Be(HttpStatusCode.Created);
        var holdId = (await hold.Content.ReadFromJsonAsync<DataResponse<HoldResult>>())!.Data.HoldId;

        await SetVenueStatusAsync(venue.LoungeId, LoungeStatus.Suspended);

        var res = await Audience().PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = holdId });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "chốt ở bước giữ chỗ không đủ — tiền thu ở bước này");
        (await res.Content.ReadAsStringAsync()).Should().Contain(BuyerMessage);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Payments.AnyAsync(p => p.ReferenceType == "TicketHold" && p.ReferenceId == holdId.ToString()))
            .Should().BeFalse("không được mở một giao dịch thanh toán nào");
    }

    [Fact]
    public async Task PhongTraDangHoatDongThiGiuChoVaTraTienBinhThuong()
    {
        var venue = await CreateVenueAsync();
        var hold = await HoldAsync(venue.OnlinePriceId);
        hold.StatusCode.Should().Be(HttpStatusCode.Created);
        var holdId = (await hold.Content.ReadFromJsonAsync<DataResponse<HoldResult>>())!.Data.HoldId;

        (await Audience().PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = holdId }))
            .IsSuccessStatusCode.Should().BeTrue("chốt mới không được chặn phòng trà đang hoạt động");
    }

    // ── Bán tại quầy: người đọc là nhân viên — nói đúng lý do ───────────────

    [Fact]
    public async Task BanTaiQuayBiTuChoiVaNoiRoLyDoChoNhanVien()
    {
        var venue = await CreateVenueAsync();
        await SetVenueStatusAsync(venue.LoungeId, LoungeStatus.Suspended);

        var res = await _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner")
            .PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = venue.CounterPriceId, Quantity = 1 });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("tạm đình chỉ", "nhân viên phải biết vì sao — khác với câu cho người mua");
        body.Should().Contain("Không thể bán vé qua hệ thống");
    }

    // ── Donation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PhongTraBiDinhChiThiKhongNhanDonation()
    {
        var venue = await CreateVenueAsync(LoungeShowStatus.Ongoing);
        await SetVenueStatusAsync(venue.LoungeId, LoungeStatus.Suspended);

        var res = await Audience().PostAsJsonAsync("/api/v1/donations",
            new { PerformanceId = venue.PerformanceId, Amount = 50_000m });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain(BuyerMessage);
    }
}
