using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Performers;

/// <summary>
/// §6.14 — Spotify/YouTube/Facebook/etc links on a performer profile. Upsert semantics: setting a
/// link for a platform the performer already has replaces it (unique index on (PerformerId,
/// Platform)). §6.12 edit rights apply here too — only Performer.CreatedByUserId or Admin may
/// add/remove links.
/// PUT /api/v1/performers/{id}/social-links | DELETE .../social-links/{linkId}
/// </summary>
[Collection("Integration")]
public sealed class PerformerSocialLinkTests
{
    private readonly ApiFactory _factory;

    public PerformerSocialLinkTests(ApiFactory factory) => _factory = factory;

    private async Task<int> CreatePerformerAsync(int ownerId, string name)
    {
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var res = await client.PostAsJsonAsync("/api/v1/performers", new
        {
            Name = name,
            AvatarUrl = (string?)null,
            Bio = "Test bio",
            Type = "Solo",
            GenreIds = Array.Empty<int>()
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    [Fact]
    public async Task AddSocialLink_ByCreator_Returns200AndAppearsInProfile()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"Spotify Artist {Guid.NewGuid():N}");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PutAsJsonAsync($"/api/v1/performers/{performerId}/social-links", new
        {
            Platform = "Spotify",
            Url = "https://open.spotify.com/artist/test",
            DisplayName = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetAsync($"/api/v1/performers/{performerId}");
        var body = await detail.Content.ReadAsStringAsync();
        body.Should().Contain("open.spotify.com/artist/test").And.Contain("Spotify");
    }

    [Fact]
    public async Task AddSocialLink_SamePlatformTwice_ReplacesRatherThanDuplicates()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"Upsert Artist {Guid.NewGuid():N}");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        await client.PutAsJsonAsync($"/api/v1/performers/{performerId}/social-links", new
        {
            Platform = "Youtube",
            Url = "https://youtube.com/@old-handle",
            DisplayName = (string?)null
        });
        var secondRes = await client.PutAsJsonAsync($"/api/v1/performers/{performerId}/social-links", new
        {
            Platform = "Youtube",
            Url = "https://youtube.com/@new-handle",
            DisplayName = "New Handle"
        });

        secondRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetAsync($"/api/v1/performers/{performerId}");
        var body = await detail.Content.ReadAsStringAsync();
        body.Should().Contain("@new-handle").And.NotContain("@old-handle");
    }

    [Fact]
    public async Task AddSocialLink_ByDifferentOwner_Returns403()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"Protected Artist {Guid.NewGuid():N}");
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await otherOwnerClient.PutAsJsonAsync($"/api/v1/performers/{performerId}/social-links", new
        {
            Platform = "Facebook",
            Url = "https://facebook.com/hijacked",
            DisplayName = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddSocialLink_InvalidPlatformName_Returns400()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"BadPlatform Artist {Guid.NewGuid():N}");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PutAsJsonAsync($"/api/v1/performers/{performerId}/social-links", new
        {
            Platform = "TikTok", // not in SocialPlatform enum
            Url = "https://tiktok.com/@test",
            DisplayName = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveSocialLink_ByCreator_RemovesFromProfile()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"Removable Artist {Guid.NewGuid():N}");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var addRes = await client.PutAsJsonAsync($"/api/v1/performers/{performerId}/social-links", new
        {
            Platform = "Instagram",
            Url = "https://instagram.com/test",
            DisplayName = (string?)null
        });
        var linkId = (await addRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        var deleteRes = await client.DeleteAsync($"/api/v1/performers/{performerId}/social-links/{linkId}");

        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await client.GetAsync($"/api/v1/performers/{performerId}");
        var body = await detail.Content.ReadAsStringAsync();
        body.Should().NotContain("instagram.com/test");
    }

    [Fact]
    public async Task RemoveSocialLink_ByAdmin_Returns204()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"Admin Removable Artist {Guid.NewGuid():N}");
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var addRes = await ownerClient.PutAsJsonAsync($"/api/v1/performers/{performerId}/social-links", new
        {
            Platform = "Soundcloud",
            Url = "https://soundcloud.com/test",
            DisplayName = (string?)null
        });
        var linkId = (await addRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var deleteRes = await adminClient.DeleteAsync($"/api/v1/performers/{performerId}/social-links/{linkId}");

        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record IdResponse(bool Success, int Data);
}
