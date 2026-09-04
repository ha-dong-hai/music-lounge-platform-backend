using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// MLACP-32 — thêm/sửa/ngưng dùng khu vực chỗ ngồi. Ngưng dùng là soft-delete (IsActive=false),
/// không xóa cứng — một zone từng bán vé vẫn phải giữ nguyên tham chiếu lịch sử.
/// POST /api/v1/lounges/{id}/zones | PUT /zones/{zoneId} | DELETE /zones/{zoneId}
/// </summary>
[Collection("Integration")]
public sealed class SeatingZoneManagementTests
{
    private readonly ApiFactory _factory;

    public SeatingZoneManagementTests(ApiFactory factory) => _factory = factory;

    private async Task<int> CreateZoneAsync(int capacity = 20)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var zone = new SeatingZone
        {
            LoungeId = SeedHelper.LoungeId, Name = $"Zone-{Guid.NewGuid():N}"[..12],
            Capacity = capacity, IsActive = true
        };
        db.SeatingZones.Add(zone);
        await db.SaveChangesAsync();
        return zone.Id;
    }

    [Fact]
    public async Task CreateZone_ByOwner_Returns201AndPersists()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/zones",
            new { Name = "VIP Balcony", Description = "Sân khấu nhìn thẳng", Capacity = 30 });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var zone = await db.SeatingZones.SingleAsync(z => z.Id == body!.Data);
        zone.Name.Should().Be("VIP Balcony");
        zone.Capacity.Should().Be(30);
        zone.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateZone_ByNonOwnerOfVenue_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/zones",
            new { Name = "Hijack Zone", Description = (string?)null, Capacity = 10 });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateZone_ByOwner_PersistsChanges()
    {
        var zoneId = await CreateZoneAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PutAsJsonAsync($"/api/v1/lounges/zones/{zoneId}",
            new { Name = "Standard Renamed", Description = "Cập nhật mô tả", Capacity = 50 });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var zone = await db.SeatingZones.SingleAsync(z => z.Id == zoneId);
        zone.Name.Should().Be("Standard Renamed");
        zone.Capacity.Should().Be(50);
    }

    [Fact]
    public async Task UpdateZone_ByNonOwnerOfVenue_Returns403()
    {
        var zoneId = await CreateZoneAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PutAsJsonAsync($"/api/v1/lounges/zones/{zoneId}",
            new { Name = "Hijacked", Description = (string?)null, Capacity = 5 });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivateZone_ByOwner_SetsInactiveAndHidesFromActiveOnlyListing()
    {
        var zoneId = await CreateZoneAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.DeleteAsync($"/api/v1/lounges/zones/{zoneId}");

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var zone = await db.SeatingZones.SingleAsync(z => z.Id == zoneId);
            zone.IsActive.Should().BeFalse("ngưng dùng là soft-delete, không xóa cứng lịch sử tham chiếu");
        }

        var listRes = await client.GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/zones?activeOnly=true");
        var listBody = await listRes.Content.ReadAsStringAsync();
        listBody.Should().NotContain($"\"id\":{zoneId}");
    }

    [Fact]
    public async Task DeactivateZone_ByNonOwnerOfVenue_Returns403()
    {
        var zoneId = await CreateZoneAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.DeleteAsync($"/api/v1/lounges/zones/{zoneId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record IdResponse(bool Success, int Data);
}
