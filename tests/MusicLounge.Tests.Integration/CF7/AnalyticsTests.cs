using System.Net;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF7;

/// <summary>
/// CF7 — Owner / Admin analytics dashboards
/// GET /api/v1/analytics/my-lounge | GET /api/v1/analytics/platform
/// </summary>
[Collection("Integration")]
public sealed class AnalyticsTests
{
    private readonly ApiFactory _factory;

    public AnalyticsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMyLounge_AsOwner_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.GetAsync($"/api/v1/analytics/my-lounge?loungeId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"totalShows\"");
    }

    [Fact]
    public async Task GetMyLounge_AsUnrelatedOwner_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.GetAsync($"/api/v1/analytics/my-lounge?loungeId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMyLounge_AsAudience_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync($"/api/v1/analytics/my-lounge?loungeId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPlatform_AsAdmin_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.GetAsync("/api/v1/analytics/platform");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"totalVenues\"");
    }

    [Fact]
    public async Task GetPlatform_AsOwner_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.GetAsync("/api/v1/analytics/platform");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
