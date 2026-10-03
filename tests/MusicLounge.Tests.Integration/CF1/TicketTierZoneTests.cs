using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// MLACP-545 — PUT /api/v1/ticket-tiers/{id}/zone: gắn khu ghế cho hạng vé tại chỗ.
/// Azure 03/10/2026: 4/4 hạng vé của 2 buổi đang mở bán chưa có khu → sơ đồ chỗ ngồi theo buổi trống, mà trước đây
/// không có đường nào gắn khu sau khi tạo (UpdateTicketTier không nhận ZoneId và khoá sau Draft).
/// </summary>
[Collection("Integration")]
public sealed class TicketTierZoneTests
{
    private readonly ApiFactory _factory;
    public TicketTierZoneTests(ApiFactory factory) => _factory = factory;

    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

    private async Task<Guid> CreateDraftShowAsync()
    {
        var res = await Owner().PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId, Name = $"Zone-{Guid.NewGuid():N}", Description = "MLACP-545", Format = "Offline",
            ScheduledStart = SeedHelper.NextShowStart(), ScheduledEnd = (DateTimeOffset?)null, CategoryId = (Guid?)null,
            OfflineQuota = 100, OnlineQuota = (int?)null, GenreIds = Array.Empty<Guid>(), MoodIds = Array.Empty<Guid>(),
            AtmosphereIds = Array.Empty<Guid>(),
            Performances = new[] { new { PerformerId = (Guid?)null, PerformerName = "Ca sĩ", Role = "Main", OrderIndex = 1, SetTime = (string?)null, AcceptsDonation = false } }
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<DataResponse<Guid>>())!.Data;
    }

    private async Task<HttpResponseMessage> CreateTierRawAsync(Guid showId, Guid? zoneId, string accessType = "Physical") =>
        await Owner().PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId, Name = "Vé VIP", Description = (string?)null, AccessType = accessType, ZoneId = zoneId,
            TotalCapacity = (int?)null,
            Prices = new[] { new { Name = "Giá thường", Price = 250_000m, Quota = (int?)null, PurchaseChannel = "Both",
                SaleStart = DateTimeOffset.UtcNow, SaleEnd = DateTimeOffset.UtcNow.AddDays(5) } }
        });

    private async Task<Guid> CreateTierAsync(Guid showId, Guid? zoneId = null, string accessType = "Physical")
    {
        var res = await CreateTierRawAsync(showId, zoneId, accessType);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<DataResponse<Guid>>())!.Data;
    }

    private async Task<Guid> CreateZoneAsync(Guid loungeId, string name = "Khu VIP")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var zone = new SeatingZone { Id = OrderedGuid.New(), LoungeId = loungeId, Name = $"{name} {Guid.NewGuid():N}"[..20], Capacity = 30,
            Layout2DX = 10, Layout2DY = 10, Layout2DWidth = 20, Layout2DHeight = 18 };
        db.SeatingZones.Add(zone);
        await db.SaveChangesAsync();
        return zone.Id;
    }

    private async Task<Guid> OtherLoungeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = OrderedGuid.New();
        db.Users.Add(new User { Id = id, Email = $"zone545-{id}@test.com", FullName = "Chủ khác" });
        db.Lounges.Add(new MusicLoungeVenue { Id = id, OwnerId = id, Name = $"Phòng trà khác {id}",
            Address = new VenueAddress { Street = "2 Test", District = "3", City = "HCM" } });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task SetShowStatusAsync(Guid showId, LoungeShowStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = await db.LoungeShows.FirstAsync(s => s.Id == showId);
        show.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<Guid?> ZoneOfAsync(Guid tierId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.TicketTiers.AsNoTracking().FirstAsync(t => t.Id == tierId)).ZoneId;
    }

    private Task<HttpResponseMessage> AssignAsync(Guid tierId, Guid zoneId, HttpClient? client = null) =>
        (client ?? Owner()).PutAsJsonAsync($"/api/v1/ticket-tiers/{tierId}/zone", new { ZoneId = zoneId });

    [Fact]
    public async Task Assign_PublishedTierWithoutZone_Succeeds_AndSeatingMapShowsZone()
    {
        var show = await CreateDraftShowAsync();
        var tier = await CreateTierAsync(show);
        var zone = await CreateZoneAsync(SeedHelper.LoungeId);
        await SetShowStatusAsync(show, LoungeShowStatus.Published);

        var res = await AssignAsync(tier, zone);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ZoneOfAsync(tier)).Should().Be(zone);
        var map = await _factory.CreateClient().GetStringAsync($"/api/v1/lounge-shows/{show}/seating-map");
        map.Should().Contain(zone.ToString(), "sơ đồ chỗ ngồi theo buổi chỉ gom khu có hạng vé gắn vào");
    }

    [Fact]
    public async Task Assign_PublishedTierAlreadyHasZone_Rejected_BuyersNotMoved()
    {
        var show = await CreateDraftShowAsync();
        var zoneA = await CreateZoneAsync(SeedHelper.LoungeId, "Khu A");
        var zoneB = await CreateZoneAsync(SeedHelper.LoungeId, "Khu B");
        var tier = await CreateTierAsync(show, zoneA);
        await SetShowStatusAsync(show, LoungeShowStatus.Published);

        var res = await AssignAsync(tier, zoneB);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ZoneOfAsync(tier)).Should().Be(zoneA);
    }

    [Fact]
    public async Task Assign_DraftTier_CanChangeZoneFreely()
    {
        var show = await CreateDraftShowAsync();
        var zoneA = await CreateZoneAsync(SeedHelper.LoungeId, "Khu A");
        var zoneB = await CreateZoneAsync(SeedHelper.LoungeId, "Khu B");
        var tier = await CreateTierAsync(show, zoneA);

        (await AssignAsync(tier, zoneB)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ZoneOfAsync(tier)).Should().Be(zoneB);
    }

    [Fact]
    public async Task Assign_ZoneOfAnotherLounge_Rejected()
    {
        var show = await CreateDraftShowAsync();
        var tier = await CreateTierAsync(show);
        var zoneKhac = await CreateZoneAsync(await OtherLoungeAsync());

        var res = await AssignAsync(tier, zoneKhac);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ZoneOfAsync(tier)).Should().BeNull();
    }

    [Fact]
    public async Task Assign_LivestreamTier_Rejected()
    {
        var show = await CreateDraftShowAsync();
        // Buổi Offline không cho TẠO hạng vé trực tuyến (422) — tạo vé tại chỗ rồi đổi loại trong DB để kiểm đúng quy tắc
        // của lệnh gắn khu, không phải quy tắc của lệnh tạo.
        var tier = await CreateTierAsync(show);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.TicketTiers.FirstAsync(t => t.Id == tier)).AccessType = AccessType.Livestream;
            await db.SaveChangesAsync();
        }
        var zone = await CreateZoneAsync(SeedHelper.LoungeId);

        (await AssignAsync(tier, zone)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Assign_CancelledShow_Rejected()
    {
        var show = await CreateDraftShowAsync();
        var tier = await CreateTierAsync(show);
        var zone = await CreateZoneAsync(SeedHelper.LoungeId);
        await SetShowStatusAsync(show, LoungeShowStatus.Cancelled);

        (await AssignAsync(tier, zone)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ZoneOfAsync(tier)).Should().BeNull();
    }

    [Fact]
    public async Task Assign_ByOwnerOfAnotherLounge_Returns403()
    {
        var show = await CreateDraftShowAsync();
        var tier = await CreateTierAsync(show);
        var zone = await CreateZoneAsync(SeedHelper.LoungeId);
        var keLa = await OtherLoungeAsync();

        var res = await AssignAsync(tier, zone, _factory.CreateAuthenticatedClient(keLa, "Owner", keLa));

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ZoneOfAsync(tier)).Should().BeNull();
    }

    [Fact]
    public async Task CreateTier_WithZoneOfAnotherLounge_Rejected()
    {
        // Lỗ hổng cũ: validator chỉ kiểm khu TỒN TẠI, không kiểm thuộc phòng trà nào.
        var show = await CreateDraftShowAsync();
        var zoneKhac = await CreateZoneAsync(await OtherLoungeAsync());

        var res = await CreateTierRawAsync(show, zoneKhac);

        res.IsSuccessStatusCode.Should().BeFalse("không được gắn khu của phòng trà khác vào hạng vé");
        ((int)res.StatusCode).Should().BeOneOf(400, 422);
    }

    private sealed record DataResponse<T>(bool Success, T Data);
}
