using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// CF4 W08 — Livestream Moderation
/// GET  /api/v1/moderations/pending
/// POST /api/v1/moderations/livestreams/{id}/review
/// </summary>
[Collection("Integration")]
public sealed class ModerationTests
{
    private readonly ApiFactory _factory;

    public ModerationTests(ApiFactory factory) => _factory = factory;

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<int> CreateFreshShowAsync()
    {
        int showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"TestShow-{Guid.NewGuid():N}",
                Description = "Integration test show",
                Format = LoungeShowFormat.Online,
                Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            showId = show.Id;
        }
        return showId;
    }

    private async Task<int> CreateLivestreamAsync()
    {
        var showId = await CreateFreshShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/livestreams",
            new { ShowId = showId });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    // ─── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPendingModerations_AsAdmin_Returns200WithList()
    {
        await CreateLivestreamAsync(); // ensure there's at least one pending
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.GetAsync("/api/v1/moderations/pending");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
    }

    [Fact]
    public async Task GetPendingModerations_AsAudience_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync("/api/v1/moderations/pending");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReviewLivestream_ApproveValid_Returns200()
    {
        var livestreamId = await CreateLivestreamAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/moderations/livestreams/{livestreamId}/review",
            new { Decision = "Approved", ReviewNote = "Looks good" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ReviewLivestream_RejectWithoutNote_Returns400()
    {
        var livestreamId = await CreateLivestreamAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/moderations/livestreams/{livestreamId}/review",
            new { Decision = "Rejected", ReviewNote = (string?)null }); // no note

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReviewLivestream_DuplicateReview_Returns409()
    {
        var livestreamId = await CreateLivestreamAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        // First review
        await client.PostAsJsonAsync(
            $"/api/v1/moderations/livestreams/{livestreamId}/review",
            new { Decision = "Approved", ReviewNote = "First pass" });

        // Duplicate review
        var res = await client.PostAsJsonAsync(
            $"/api/v1/moderations/livestreams/{livestreamId}/review",
            new { Decision = "Rejected", ReviewNote = "Changed mind" });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReviewLivestream_InvalidDecision_Returns400()
    {
        var livestreamId = await CreateLivestreamAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/moderations/livestreams/{livestreamId}/review",
            new { Decision = "Terminated", ReviewNote = "hack attempt" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record IdResponse(bool Success, int Data);
}
