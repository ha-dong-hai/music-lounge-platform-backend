using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Performers;

/// <summary>
/// §6.12 — Performers is a catalog shared across all Owners: CREATE/READ/ASSIGN open to any
/// Owner, EDIT restricted to created_by_user_id + Admin.
/// POST/GET/PUT /api/v1/performers
/// </summary>
[Collection("Integration")]
public sealed class PerformerTests
{
    private readonly ApiFactory _factory;

    public PerformerTests(ApiFactory factory) => _factory = factory;

    private async Task<int> CreatePerformerAsync(int ownerId, string name, params int[] genreIds)
    {
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var res = await client.PostAsJsonAsync("/api/v1/performers", new
        {
            Name = name,
            AvatarUrl = (string?)null,
            Bio = "Test bio",
            Type = "Solo",
            GenreIds = genreIds
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    [Fact]
    public async Task CreatePerformer_AsOwner_Returns201()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/performers", new
        {
            Name = $"New Artist {Guid.NewGuid():N}",
            AvatarUrl = (string?)null,
            Bio = (string?)null,
            Type = "Band",
            GenreIds = Array.Empty<int>()
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePerformer_AsAudience_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/performers", new
        {
            Name = "Should not be created",
            AvatarUrl = (string?)null,
            Bio = (string?)null,
            Type = "Solo",
            GenreIds = Array.Empty<int>()
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPerformers_SearchByName_FindsCreatedPerformer()
    {
        var uniqueName = $"Searchable Artist {Guid.NewGuid():N}";
        await CreatePerformerAsync(SeedHelper.OwnerId, uniqueName);

        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");
        var res = await client.GetAsync($"/api/v1/performers?search={Uri.EscapeDataString(uniqueName)}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(uniqueName);
    }

    [Fact]
    public async Task GetPerformerById_ReturnsAssignedGenres()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"Genred Artist {Guid.NewGuid():N}", SeedHelper.GenreId1);

        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var res = await client.GetAsync($"/api/v1/performers/{performerId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"genreIds\":[1]");
    }

    [Fact]
    public async Task UpdatePerformer_ByCreator_Returns204()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"Editable Artist {Guid.NewGuid():N}");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PutAsJsonAsync($"/api/v1/performers/{performerId}", new
        {
            Name = "Updated Name",
            AvatarUrl = (string?)null,
            Bio = "Updated bio",
            Type = "Band",
            GenreIds = new[] { SeedHelper.GenreId2 }
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await client.GetAsync($"/api/v1/performers/{performerId}");
        var body = await detail.Content.ReadAsStringAsync();
        body.Should().Contain("Updated Name").And.Contain("\"genreIds\":[2]");
    }

    [Fact]
    public async Task UpdatePerformer_ByDifferentOwner_Returns403()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"Protected Artist {Guid.NewGuid():N}");
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await otherOwnerClient.PutAsJsonAsync($"/api/v1/performers/{performerId}", new
        {
            Name = "Hijacked Name",
            AvatarUrl = (string?)null,
            Bio = (string?)null,
            Type = "Solo",
            GenreIds = Array.Empty<int>()
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePerformer_ByAdmin_Returns204()
    {
        var performerId = await CreatePerformerAsync(SeedHelper.OwnerId, $"Admin Editable Artist {Guid.NewGuid():N}");
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PutAsJsonAsync($"/api/v1/performers/{performerId}", new
        {
            Name = "Admin-corrected Name",
            AvatarUrl = (string?)null,
            Bio = (string?)null,
            Type = "Solo",
            GenreIds = Array.Empty<int>()
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record IdResponse(bool Success, int Data);
}
