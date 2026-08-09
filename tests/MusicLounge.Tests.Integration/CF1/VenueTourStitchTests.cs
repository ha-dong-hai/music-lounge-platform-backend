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
/// Alternative to AddTourScene for Owners without a native 360° capture app — stitches several
/// overlapping photos into one panorama via the standalone panorama-stitcher microservice.
/// No PanoramaStitcher:BaseUrl is configured in appsettings.Testing.json, so every attempt here
/// exercises the "vendor unavailable" path — proving the fail-closed design holds: a failed
/// attempt must never consume the Owner's MaxTourScenes quota.
/// POST /api/v1/lounges/{id}/tour/scenes/stitch
/// </summary>
[Collection("Integration")]
public sealed class VenueTourStitchTests
{
    private readonly ApiFactory _factory;
    private static int _freshIdCounter = 9500;

    public VenueTourStitchTests(ApiFactory factory) => _factory = factory;

    private async Task<(int OwnerId, int LoungeId)> CreateOwnerWithLoungeAsync(int maxTourScenes = 5)
    {
        var id = Interlocked.Increment(ref _freshIdCounter);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Users.Add(new User { Id = id, Email = $"stitcho{id}@test.com", FullName = "Stitch Owner" });
        db.Lounges.Add(new MusicLoungeVenue
        {
            Id = id, OwnerId = id, Name = $"Stitch Lounge {id}",
            Address = new VenueAddress { Street = "1 Test", District = "1", City = "HCM" }
        });
        db.SubscriptionPackages.Add(new SubscriptionPackage
        {
            Id = id, Name = $"StitchPkg-{id}", Price = 500_000m,
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

    private static object StitchBody(int photoCount = 3, string? name = null) => new
    {
        SourceImageUrls = Enumerable.Range(1, photoCount)
            .Select(i => $"https://cdn.example.com/raw-{Guid.NewGuid():N}-{i}.jpg").ToArray(),
        Name = name
    };

    [Fact]
    public async Task StitchTourScene_VendorUnavailable_LogsFailedAttempt_DoesNotConsumeQuota_Returns503()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", StitchBody());

        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempt = await db.VenueTourStitchAttempts.FirstOrDefaultAsync(a => a.LoungeId == loungeId);
        attempt.Should().NotBeNull();
        attempt!.Status.Should().Be(VenueTourStitchStatus.Failed);
        attempt.ResultSceneId.Should().BeNull();

        var scenes = await db.VenueTourScenes.Where(s => s.LoungeId == loungeId).ToListAsync();
        scenes.Should().BeEmpty("a failed stitch must not create a scene or otherwise touch the quota");
    }

    [Fact]
    public async Task StitchTourScene_ByNonOwnerOfVenue_Returns403()
    {
        var (_, loungeId) = await CreateOwnerWithLoungeAsync();
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await otherOwnerClient.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", StitchBody());

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StitchTourScene_FewerThanTwoImages_Returns400()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch",
            StitchBody(photoCount: 1));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StitchTourScene_AlreadyAtSceneQuota_Returns422WithoutCallingVendor()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync(maxTourScenes: 1);
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes",
            new { ImageUrl = "https://cdn.example.com/pano.jpg", Name = (string?)null });

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", StitchBody());

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("giới hạn");

        // The quota check must short-circuit BEFORE calling the (unavailable) vendor — no attempt
        // log should even be written for a request that was always going to be rejected on quota.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempts = await db.VenueTourStitchAttempts.Where(a => a.LoungeId == loungeId).ToListAsync();
        attempts.Should().BeEmpty();
    }

    [Fact]
    public async Task StitchTourScene_ExceedsAntiAbuseAttemptCap_ReturnsClearLimitError()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync(maxTourScenes: 50);
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        // Uses the fallback default (20, ISystemConfigService.GetIntAsync's default when no
        // system_config row exists) rather than inserting an override row — ISystemConfigService
        // caches by config KEY for 60s process-wide, so an override written here could be masked
        // by an earlier test's read of the same (global, not per-lounge) key within that window.
        // Every call fails (no PanoramaStitcher:BaseUrl configured) but still counts toward the
        // per-lounge anti-abuse limit.
        for (var i = 0; i < 20; i++)
            await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", StitchBody());

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", StitchBody());

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("giới hạn");
    }
}
