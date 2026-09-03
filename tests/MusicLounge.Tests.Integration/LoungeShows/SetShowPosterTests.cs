using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// Manual counterpart to POST {id}/ai-poster — an Owner who wants to upload their own poster
/// instead of generating one (or doesn't have the AI-poster subscription tier at all).
/// PUT /api/v1/lounge-shows/{id}/poster
/// </summary>
[Collection("Integration")]
public sealed class SetShowPosterTests
{
    private readonly ApiFactory _factory;

    public SetShowPosterTests(ApiFactory factory) => _factory = factory;

    private async Task<int> CreateShowAsync(int ownerId = SeedHelper.OwnerId)
    {
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner", SeedHelper.LoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"ManualPosterTestShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = "Offline",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(14),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    [Fact]
    public async Task SetPoster_AsOwner_SetsUrlAndClearsPosterByAi()
    {
        var showId = await CreateShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/poster",
            new { ImageUrl = "https://cdn.example.com/manual-poster.jpg" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = await db.LoungeShows.FirstAsync(s => s.Id == showId);
        show.PosterUrl.Should().Be("https://cdn.example.com/manual-poster.jpg");
        show.PosterByAi.Should().BeFalse("manually uploaded posters are never marked AI-generated");
    }

    [Fact]
    public async Task SetPoster_AsNonOwnerOfVenue_Returns403()
    {
        var showId = await CreateShowAsync();
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await otherOwnerClient.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/poster",
            new { ImageUrl = "https://cdn.example.com/hijacked.jpg" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetPoster_AfterAiGeneratedOneAlreadySet_OverwritesAndClearsAiFlag()
    {
        var showId = await CreateShowAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = await db.LoungeShows.FirstAsync(s => s.Id == showId);
            show.PosterUrl = "https://cdn.example.com/ai-poster.jpg";
            show.PosterByAi = true;
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/poster",
            new { ImageUrl = "https://cdn.example.com/manual-override.jpg" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await verifyDb.LoungeShows.FirstAsync(s => s.Id == showId);
        reloaded.PosterUrl.Should().Be("https://cdn.example.com/manual-override.jpg");
        reloaded.PosterByAi.Should().BeFalse();
    }

    private sealed record IdResponse(bool Success, int Data);
}
