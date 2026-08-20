using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// Alternative to AddTourScene for Owners without a native 360° capture app — stitches several
/// overlapping photos into one panorama via the standalone panorama-stitcher microservice.
///
/// Runs in the background (StitchVenueTourSceneJob): the HTTP endpoint only does the upfront
/// checks (ownership/quota/anti-abuse) and creates a Pending VenueTourStitchAttempt row, returning
/// 202 with its id immediately — the actual stitch happens when the job runs. Tests that need to
/// observe the job's outcome resolve it directly from DI and call ExecuteAsync manually (same
/// pattern ModerationAiScoringTests uses for ScoreModerationWithAiJob), rather than relying on
/// Hangfire's own worker to have picked it up by the time the test asserts.
///
/// No PanoramaStitcher:BaseUrl is configured in appsettings.Testing.json, so running the job here
/// always exercises the "vendor unavailable" path — proving the fail-closed design holds: a failed
/// attempt must never consume the Owner's MaxTourScenes quota.
/// POST /api/v1/lounges/{id}/tour/scenes/stitch | GET .../tour/scenes/stitch/{attemptId}
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

    // SourceImageUrls must look like our own "/uploads/..." paths — StitchVenueTourSceneCommand
    // Validator rejects anything else outright (SSRF gate: the panorama-stitcher would otherwise
    // fetch whatever URL it's given, no network restriction of its own). These don't need to be
    // real files on disk — the stitch call itself always fails first in this test environment
    // (no PanoramaStitcher:BaseUrl configured), so nothing ever tries to read them.
    private static object StitchBody(int photoCount = 3, string? name = null) => new
    {
        SourceImageUrls = Enumerable.Range(1, photoCount)
            .Select(i => $"/uploads/raw-{Guid.NewGuid():N}-{i}.jpg").ToArray(),
        Name = name
    };

    private async Task<string> UploadRealImageAsync(HttpClient client)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", $"pano-{Guid.NewGuid():N}.png");

        var res = await client.PostAsync("/api/v1/uploads/images", form);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<UploadResponse>();
        return body!.Data.Url;
    }

    private async Task RunJobAsync(int attemptId, int loungeId, IReadOnlyList<string> sourceImageUrls, string? name)
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<StitchVenueTourSceneJob>();
        await job.ExecuteAsync(attemptId, loungeId, sourceImageUrls, name, new JobCancellationToken(false));
    }

    [Fact]
    public async Task StitchTourScene_Enqueues_Returns202WithPendingAttempt()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", StitchBody());

        res.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var attemptId = (await res.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempt = await db.VenueTourStitchAttempts.FirstAsync(a => a.Id == attemptId);
        attempt.LoungeId.Should().Be(loungeId);
        attempt.Status.Should().Be(VenueTourStitchStatus.Pending);
    }

    [Fact]
    public async Task StitchVenueTourSceneJob_VendorUnavailable_MarksAttemptFailed_DoesNotConsumeQuota()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var urls = new List<string> { "/uploads/raw-a.jpg", "/uploads/raw-b.jpg", "/uploads/raw-c.jpg" };

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch",
            new { SourceImageUrls = urls, Name = (string?)null });
        var attemptId = (await res.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        await RunJobAsync(attemptId, loungeId, urls, null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempt = await db.VenueTourStitchAttempts.FirstAsync(a => a.Id == attemptId);
        attempt.Status.Should().Be(VenueTourStitchStatus.Failed);
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
    public async Task StitchTourScene_SourceUrlOutsideUploads_Returns400()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", new
        {
            SourceImageUrls = new[] { "https://attacker.example/x.jpg", "https://attacker.example/y.jpg" },
            Name = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "SourceImageUrls must be our own /uploads/... paths - the stitcher would otherwise fetch " +
            "whatever URL an Owner gives it (SSRF)");
    }

    [Fact]
    public async Task StitchTourScene_AlreadyAtSceneQuota_Returns422WithoutCreatingAttempt()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync(maxTourScenes: 1);
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client);
        await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes",
            new { ImageUrl = imageUrl, Name = (string?)null });

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", StitchBody());

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("giới hạn");

        // The quota check must short-circuit BEFORE creating a Pending attempt/enqueueing anything
        // — no attempt log should even be written for a request that was always going to be rejected.
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
        // Every call creates a Pending attempt (enqueueing is enough to count toward the cap - the
        // job itself is never run here), so 20 calls exhausts the fallback default.
        for (var i = 0; i < 20; i++)
            await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", StitchBody());

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", StitchBody());

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("giới hạn");
    }

    private sealed record IdResponse(bool Success, int Data);
    private sealed record UploadResponse(bool Success, UploadedUrl Data);
    private sealed record UploadedUrl(string Url);
}
