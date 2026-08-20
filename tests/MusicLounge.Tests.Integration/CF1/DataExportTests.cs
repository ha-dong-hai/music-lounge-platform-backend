using System.Net;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// DSAR (91/2025/QH15) data-portability endpoint — governance gap #1 from the 2026-08-09
/// production-hardening audit.
/// GET /api/v1/me/data-export
/// </summary>
[Collection("Integration")]
public sealed class DataExportTests
{
    private readonly ApiFactory _factory;

    public DataExportTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMyDataExport_Authenticated_Returns200WithOwnData()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync("/api/v1/me/data-export");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"email\":\"audience@test.com\"");
        // AudienceId already has a Confirmed ticket seeded (SeedHelper.AudienceTicketId) — must appear.
        body.Should().Contain(SeedHelper.AudienceTicketId.ToString());
    }

    [Fact]
    public async Task GetMyDataExport_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/me/data-export");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
