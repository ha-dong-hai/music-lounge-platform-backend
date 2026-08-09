using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// 360° panorama venue tour (Louvre/HCMC-Museum style — distinct from MusicLounge.Model3DUrl's
/// single .glb file). Scenes are gated by the Owner's active subscription (MaxTourScenesSnapshot,
/// D12 snapshot pattern). Hotspots link scenes together (Navigate) or show static text (Info).
///
/// Every test builds its OWN fresh owner+lounge+subscription rather than reusing
/// SeedHelper.LoungeId/OwnerId — this suite's own quota-exhaustion test would otherwise
/// permanently use up the shared seed lounge's scene quota for the rest of the (shared-DB) test run.
/// GET /api/v1/lounges/{id}/tour | POST/DELETE .../tour/scenes | POST/DELETE .../tour/hotspots
/// </summary>
[Collection("Integration")]
public sealed class VenueTourTests
{
    private readonly ApiFactory _factory;
    private static int _freshIdCounter = 9300;

    public VenueTourTests(ApiFactory factory) => _factory = factory;

    /// <summary>Fresh Owner + Lounge + an Active subscription granting maxTourScenes.</summary>
    private async Task<(int OwnerId, int LoungeId)> CreateOwnerWithLoungeAsync(int maxTourScenes = 5)
    {
        var id = Interlocked.Increment(ref _freshIdCounter);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Users.Add(new User { Id = id, Email = $"touro{id}@test.com", FullName = "Tour Owner" });
        db.Lounges.Add(new MusicLoungeVenue
        {
            Id = id, OwnerId = id, Name = $"Tour Lounge {id}",
            Address = new VenueAddress { Street = "1 Test", District = "1", City = "HCM" }
        });
        db.SubscriptionPackages.Add(new SubscriptionPackage
        {
            Id = id, Name = $"TourPkg-{id}", Price = 500_000m,
            BillingCycle = SubscriptionBillingCycle.Monthly,
            MaxTicketsPerEvent = 1000, MaxTourScenes = maxTourScenes, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.OwnerSubscriptions.Add(new OwnerSubscription
        {
            Id = id, OwnerId = id, PackageId = id,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-1), ExpiresAt = DateTimeOffset.UtcNow.AddDays(29),
            Status = SubscriptionStatus.Active,
            MaxTicketsPerEventSnapshot = 1000, MaxTourScenesSnapshot = maxTourScenes
        });
        await db.SaveChangesAsync();
        return (id, id);
    }

    private async Task<int> AddSceneAsync(HttpClient ownerClient, int loungeId, string? name = null)
    {
        var res = await ownerClient.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new
        {
            ImageUrl = $"https://cdn.example.com/pano-{Guid.NewGuid():N}.jpg",
            Name = name
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    [Fact]
    public async Task AddTourScene_ByOwner_Returns201()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new
        {
            ImageUrl = "https://cdn.example.com/pano-entrance.jpg",
            Name = "Sảnh chính"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddTourScene_ByNonOwnerOfVenue_Returns403()
    {
        var (_, loungeId) = await CreateOwnerWithLoungeAsync();
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await otherOwnerClient.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new
        {
            ImageUrl = "https://cdn.example.com/hijacked.jpg",
            Name = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddTourScene_ExceedsSubscriptionQuota_Returns422()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync(maxTourScenes: 2);
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        await AddSceneAsync(client, loungeId);
        await AddSceneAsync(client, loungeId);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new
        {
            ImageUrl = "https://cdn.example.com/pano-overflow.jpg",
            Name = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("giới hạn");
    }

    [Fact]
    public async Task AddTourScene_NoActiveSubscription_Returns422()
    {
        var id = Interlocked.Increment(ref _freshIdCounter);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = id, Email = $"notour{id}@test.com", FullName = "NoTour Owner" });
            db.Lounges.Add(new MusicLoungeVenue
            {
                Id = id, OwnerId = id, Name = "NoTour Lounge",
                Address = new VenueAddress { Street = "1 Test", District = "1", City = "HCM" }
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(id, "Owner");
        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{id}/tour/scenes", new
        {
            ImageUrl = "https://cdn.example.com/pano.jpg",
            Name = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetTour_Anonymous_ReturnsScenesWithHotspots()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var scene1 = await AddSceneAsync(client, loungeId, "Sảnh chính");
        var scene2 = await AddSceneAsync(client, loungeId, "Sân khấu");

        var navRes = await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{scene1}/hotspots", new
            {
                Type = "Navigate", Yaw = 45.0, Pitch = 0.0, Label = "Đi tới sân khấu",
                TargetSceneId = scene2, InfoText = (string?)null
            });
        navRes.StatusCode.Should().Be(HttpStatusCode.Created);

        await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{scene1}/hotspots", new
            {
                Type = "Info", Yaw = -90.0, Pitch = 10.0, Label = "Quầy bar",
                TargetSceneId = (int?)null, InfoText = "Quầy bar phục vụ 18h-24h"
            });

        var anonClient = _factory.CreateClient();
        var res = await anonClient.GetAsync($"/api/v1/lounges/{loungeId}/tour");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Sảnh chính").And.Contain("Sân khấu")
            .And.Contain("\"type\":\"Navigate\"").And.Contain("\"type\":\"Info\"")
            .And.Contain("Quầy bar phục vụ 18h-24h");
    }

    [Fact]
    public async Task AddTourHotspot_NavigateWithoutTargetSceneId_Returns400()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);

        var res = await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", new
            {
                Type = "Navigate", Yaw = 0.0, Pitch = 0.0, Label = (string?)null,
                TargetSceneId = (int?)null, InfoText = (string?)null
            });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTourHotspot_TargetSceneFromDifferentLounge_Returns404()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var (otherOwnerId, otherLoungeId) = await CreateOwnerWithLoungeAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var otherOwnerClient = _factory.CreateAuthenticatedClient(otherOwnerId, "Owner");
        var sceneInMyLounge = await AddSceneAsync(ownerClient, loungeId);
        var sceneInOtherLounge = await AddSceneAsync(otherOwnerClient, otherLoungeId);

        var res = await ownerClient.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneInMyLounge}/hotspots", new
            {
                Type = "Navigate", Yaw = 0.0, Pitch = 0.0, Label = (string?)null,
                TargetSceneId = sceneInOtherLounge, InfoText = (string?)null
            });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveTourScene_AlsoRemovesHotspotsThatNavigatedToIt()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneA = await AddSceneAsync(client, loungeId);
        var sceneB = await AddSceneAsync(client, loungeId);
        var hotspotRes = await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneA}/hotspots", new
            {
                Type = "Navigate", Yaw = 0.0, Pitch = 0.0, Label = (string?)null,
                TargetSceneId = sceneB, InfoText = (string?)null
            });
        var hotspotId = (await hotspotRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        // Deleting the TARGET must not throw an FK violation (TargetSceneId is Restrict, not
        // Cascade — two cascade paths into the same table isn't allowed by SQL Server), and must
        // clean up the now-dangling hotspot in scene A rather than leaving it pointing nowhere.
        var deleteRes = await client.DeleteAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneB}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var danglingHotspot = await db.VenueTourHotspots.FindAsync(hotspotId);
        danglingHotspot.Should().BeNull("the hotspot that navigated to the deleted scene must be cleaned up too");
    }

    [Fact]
    public async Task RemoveTourHotspot_ByOwner_Returns204()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);
        var hotspotRes = await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", new
            {
                Type = "Info", Yaw = 0.0, Pitch = 0.0, Label = (string?)null,
                TargetSceneId = (int?)null, InfoText = "Test info"
            });
        var hotspotId = (await hotspotRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        var res = await client.DeleteAsync($"/api/v1/lounges/{loungeId}/tour/hotspots/{hotspotId}");

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SetTourScenePosition_ByOwner_PersistsCoordinatesAndSurfacesFloorPlanImage()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        await client.PutAsJsonAsync($"/api/v1/lounges/{loungeId}/area-layout-image",
            new { ImageUrl = "https://cdn.example.com/floorplan.jpg" });
        var sceneId = await AddSceneAsync(client, loungeId);

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/position", new { X = 40.5, Y = 60.25 });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonClient = _factory.CreateClient();
        var tourRes = await anonClient.GetAsync($"/api/v1/lounges/{loungeId}/tour");
        var body = await tourRes.Content.ReadAsStringAsync();
        body.Should().Contain("floorplan.jpg").And.Contain("\"positionX\":40.5").And.Contain("\"positionY\":60.25");
    }

    [Fact]
    public async Task SetTourScenePosition_OnlyXProvided_Returns400()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/position", new { X = 40.0, Y = (double?)null });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetTourScenePosition_BothNull_ClearsExistingPosition()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);
        await client.PutAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/position", new { X = 10.0, Y = 20.0 });

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/position", new { X = (double?)null, Y = (double?)null });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scene = await db.VenueTourScenes.FindAsync(sceneId);
        scene!.PositionX.Should().BeNull();
        scene.PositionY.Should().BeNull();
    }

    private sealed record IdResponse(bool Success, int Data);
}
