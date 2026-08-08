using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// Chọn khu vực trực quan (2D/3D) — layout coordinates trên SeatingZone + bản đồ tổng hợp theo show.
/// PUT /api/v1/lounges/{id}/zones/{zoneId}/layout-2d | layout-3d | PUT /area-layout-image
/// GET /api/v1/lounge-shows/{id}/seating-map
/// </summary>
[Collection("Integration")]
public sealed class SeatingZoneLayoutTests
{
    private readonly ApiFactory _factory;

    public SeatingZoneLayoutTests(ApiFactory factory) => _factory = factory;

    private async Task<int> CreateZoneAsync(int capacity = 20, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var zone = new SeatingZone
        {
            LoungeId = SeedHelper.LoungeId, Name = $"Zone-{Guid.NewGuid():N}"[..12],
            Capacity = capacity, IsActive = isActive
        };
        db.SeatingZones.Add(zone);
        await db.SaveChangesAsync();
        return zone.Id;
    }

    [Fact]
    public async Task SetZoneLayout2D_ByOwner_PersistsCoordinates()
    {
        var zoneId = await CreateZoneAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/zones/{zoneId}/layout-2d",
            new { X = 10.5, Y = 20.5, Width = 30, Height = 15, RotationDeg = 0, Color = "#FF8800" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var zone = await db.SeatingZones.SingleAsync(z => z.Id == zoneId);
        zone.Layout2DX.Should().Be(10.5);
        zone.Layout2DWidth.Should().Be(30);
        zone.LayoutColor.Should().Be("#FF8800");
    }

    [Fact]
    public async Task SetZoneLayout2D_ByNonOwner_Returns403()
    {
        var zoneId = await CreateZoneAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/zones/{zoneId}/layout-2d",
            new { X = 10, Y = 10, Width = 10, Height = 10, RotationDeg = 0, Color = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetZoneLayout2D_CoordinateOutOfRange_Returns400()
    {
        var zoneId = await CreateZoneAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/zones/{zoneId}/layout-2d",
            new { X = 150, Y = 10, Width = 10, Height = 10, RotationDeg = 0, Color = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetZoneLayout3D_SetThenClear_RoundTripsCorrectly()
    {
        var zoneId = await CreateZoneAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var setRes = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/zones/{zoneId}/layout-3d",
            new { X = 1.5, Y = 0.0, Z = -2.5 });
        setRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var zone = await db.SeatingZones.SingleAsync(z => z.Id == zoneId);
            zone.Layout3DX.Should().Be(1.5);
            zone.Layout3DZ.Should().Be(-2.5);
        }

        var clearRes = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/zones/{zoneId}/layout-3d",
            new { X = (double?)null, Y = (double?)null, Z = (double?)null });
        clearRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var zone = await db.SeatingZones.SingleAsync(z => z.Id == zoneId);
            zone.Layout3DX.Should().BeNull();
        }
    }

    [Fact]
    public async Task SetZoneLayout3D_PartialCoordinates_Returns400()
    {
        var zoneId = await CreateZoneAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/zones/{zoneId}/layout-3d",
            new { X = 1.0, Y = (double?)null, Z = (double?)null });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetAreaLayoutImage_ByOwner_PersistsUrl()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/area-layout-image",
            new { ImageUrl = "/uploads/floorplan-test.png" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = await db.Lounges.SingleAsync(l => l.Id == SeedHelper.LoungeId);
        lounge.AreaLayoutImageUrl.Should().Be("/uploads/floorplan-test.png");

        // Don dep — tranh anh huong cac test khac dung chung SeedHelper.LoungeId trong cung 1 lan chay.
        await client.PutAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/area-layout-image", new { ImageUrl = (string?)null });
    }

    [Fact]
    public async Task GetSeatingMap_AggregatesAvailabilityAndPriceRange_OnlyForZonesUsedInThisShow()
    {
        int zoneWithTierId, zoneWithoutTierId, tierId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var zoneWithTier = new SeatingZone
            {
                LoungeId = SeedHelper.LoungeId, Name = $"VIP-{Guid.NewGuid():N}"[..10],
                Capacity = 10, DisplayOrder = 0, LayoutColor = "#AA0000"
            };
            var zoneWithoutTier = new SeatingZone
            {
                LoungeId = SeedHelper.LoungeId, Name = $"Unused-{Guid.NewGuid():N}"[..10],
                Capacity = 5, DisplayOrder = 1
            };
            db.SeatingZones.AddRange(zoneWithTier, zoneWithoutTier);
            await db.SaveChangesAsync();
            zoneWithTierId = zoneWithTier.Id;
            zoneWithoutTierId = zoneWithoutTier.Id;

            var tier = new TicketTier
            {
                LoungeShowId = SeedHelper.ShowId, Name = $"Zoned-{Guid.NewGuid():N}"[..10],
                AccessType = AccessType.Physical, ZoneId = zoneWithTierId
            };
            db.TicketTiers.Add(tier);
            await db.SaveChangesAsync();
            tierId = tier.Id;

            var priceEarly = new TicketPrice
            {
                TierId = tierId, Name = "Sớm", Price = 100_000m, Quota = 10,
                PurchaseChannel = PurchaseChannel.Online,
                SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(5)
            };
            var priceStandard = new TicketPrice
            {
                TierId = tierId, Name = "Thường", Price = 150_000m, Quota = 5,
                PurchaseChannel = PurchaseChannel.Online,
                SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(5)
            };
            db.TicketPrices.AddRange(priceEarly, priceStandard);
            await db.SaveChangesAsync();

            // Availability được tính live từ tickets Confirmed/Pending thực tế (không còn đọc
            // TicketPrice.Sold — xem ghi chú trên entity) nên seed đúng 3 và 1 vé đã bán thay vì
            // gán thẳng vào cột đếm.
            db.Tickets.AddRange(
                Enumerable.Range(0, 3).Select(_ => new Ticket
                {
                    Id = Guid.NewGuid(), PriceId = priceEarly.Id, TierId = tierId, ShowId = SeedHelper.ShowId,
                    Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online,
                    CreatedAt = DateTimeOffset.UtcNow
                }).Concat(Enumerable.Range(0, 1).Select(_ => new Ticket
                {
                    Id = Guid.NewGuid(), PriceId = priceStandard.Id, TierId = tierId, ShowId = SeedHelper.ShowId,
                    Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online,
                    CreatedAt = DateTimeOffset.UtcNow
                })));
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/api/v1/lounge-shows/{SeedHelper.ShowId}/seating-map");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<SeatingMapResponse>();
        body!.Data.Zones.Should().ContainSingle(z => z.ZoneId == zoneWithTierId);
        body.Data.Zones.Should().NotContain(z => z.ZoneId == zoneWithoutTierId);

        var entry = body.Data.Zones.Single(z => z.ZoneId == zoneWithTierId);
        entry.AvailableCount.Should().Be((10 - 3) + (5 - 1)); // 11
        entry.MinPrice.Should().Be(100_000m);
        entry.MaxPrice.Should().Be(150_000m);
        entry.Color.Should().Be("#AA0000");
    }

    [Fact]
    public async Task GetSeatingMap_ZoneDeactivatedAfterShowSoldTickets_StillAppears()
    {
        int zoneId, tierId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var zone = new SeatingZone
            {
                LoungeId = SeedHelper.LoungeId, Name = $"WillDeactivate-{Guid.NewGuid():N}"[..8],
                Capacity = 8
            };
            db.SeatingZones.Add(zone);
            await db.SaveChangesAsync();
            zoneId = zone.Id;

            var tier = new TicketTier
            {
                LoungeShowId = SeedHelper.ShowId, Name = $"DeactZoneTier-{Guid.NewGuid():N}"[..8],
                AccessType = AccessType.Physical, ZoneId = zoneId
            };
            db.TicketTiers.Add(tier);
            await db.SaveChangesAsync();
            tierId = tier.Id;

            db.TicketPrices.Add(new TicketPrice
            {
                TierId = tierId, Name = "Vé", Price = 80_000m, Quota = 8, Sold = 2,
                PurchaseChannel = PurchaseChannel.Online,
                SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(5)
            });
            await db.SaveChangesAsync();

            // Owner deactivate zone SAU KHI show da co tier tham chieu — khong duoc bien mat khoi ban do.
            zone.IsActive = false;
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/api/v1/lounge-shows/{SeedHelper.ShowId}/seating-map");
        var body = await res.Content.ReadFromJsonAsync<SeatingMapResponse>();

        body!.Data.Zones.Should().ContainSingle(z => z.ZoneId == zoneId);
    }

    [Fact]
    public async Task GetSeatingMap_ForDraftShow_ByAnonymous_Returns404()
    {
        int draftShowId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var draftShow = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId, Name = $"Draft-{Guid.NewGuid():N}"[..10],
                Description = "Draft", Format = LoungeShowFormat.Online,
                Status = LoungeShowStatus.Draft, ScheduledStart = DateTimeOffset.UtcNow.AddDays(3)
            };
            db.LoungeShows.Add(draftShow);
            await db.SaveChangesAsync();
            draftShowId = draftShow.Id;
        }

        var anonRes = await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{draftShowId}/seating-map");
        anonRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var otherOwnerRes = await _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner")
            .GetAsync($"/api/v1/lounge-shows/{draftShowId}/seating-map");
        otherOwnerRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerRes = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner")
            .GetAsync($"/api/v1/lounge-shows/{draftShowId}/seating-map");
        ownerRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record SeatingMapResponse(bool Success, SeatingMapData Data);
    private sealed record SeatingMapData(string? AreaLayoutImageUrl, List<ZoneMapEntry> Zones);
    private sealed record ZoneMapEntry(
        int ZoneId, string Name, int Capacity, string? Color,
        double? Layout2DX, double? Layout2DY, double? Layout2DWidth, double? Layout2DHeight, double? Layout2DRotationDeg,
        double? Layout3DX, double? Layout3DY, double? Layout3DZ,
        int? AvailableCount, decimal? MinPrice, decimal? MaxPrice);
}
