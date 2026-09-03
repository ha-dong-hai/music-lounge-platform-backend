using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF2;

/// <summary>
/// BR-24/BR-25 — GET /api/v1/recommendations. Found broken during the master-backend-techlead
/// review's sweep of controllers with zero test coverage: GetRecommendedLoungeShowsQueryHandler
/// falls back to LoungeShowRepository.GetTrendingAsync for any user without ai_consent — which is
/// every user by default (BVDLCN 2025 opt-out) — and that query threw a 500 on every call. See
/// LoungeShowRepository.GetTrendingAsync for why.
/// </summary>
[Collection("Integration")]
public sealed class RecommendationsTests
{
    private readonly ApiFactory _factory;

    public RecommendationsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetRecommended_UserWithoutAiConsent_FallsBackToTrendingSuccessfully()
    {
        // SeedHelper.AudienceId has ai_consent = false (BVDLCN 2025 default) and ShowId=1 is
        // seeded Published/Ongoing — this exercises the trending-fallback path end-to-end.
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync("/api/v1/recommendations?limit=5");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
    }
}
