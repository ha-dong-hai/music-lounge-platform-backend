using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF2;

/// <summary>
/// CF2 W10 — AI Preferences (PUT /api/v1/me/preferences)
/// </summary>
[Collection("Integration")]
public sealed class AiPreferencesTests
{
    private readonly ApiFactory _factory;

    public AiPreferencesTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task UpdatePreferences_WithValidData_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var response = await client.PutAsJsonAsync("/api/v1/me/preferences", new
        {
            GenreIds = new[] { SeedHelper.GenreId1, SeedHelper.GenreId2 },
            MoodIds = new[] { SeedHelper.MoodId1 },
            AtmosphereIds = new[] { SeedHelper.AtmosphereId1 },
            EnableAiConsent = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePreferences_ClearAll_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var response = await client.PutAsJsonAsync("/api/v1/me/preferences", new
        {
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            EnableAiConsent = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePreferences_WithNonExistentGenreId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var response = await client.PutAsJsonAsync("/api/v1/me/preferences", new
        {
            GenreIds = new[] { 99999 }, // does not exist
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            EnableAiConsent = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdatePreferences_WithMoreThan10Genres_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var response = await client.PutAsJsonAsync("/api/v1/me/preferences", new
        {
            GenreIds = Enumerable.Range(1, 11).ToArray(), // 11 items — exceeds max
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            EnableAiConsent = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdatePreferences_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient(); // no auth headers

        var response = await client.PutAsJsonAsync("/api/v1/me/preferences", new
        {
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            EnableAiConsent = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
