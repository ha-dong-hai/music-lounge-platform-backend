using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF7;

/// <summary>
/// master-backend-techlead review. Correction from the first pass of this review: every one of
/// the ~21 paginated query handlers actually DOES clamp PageSize already (Math.Clamp/Math.Min
/// inline in the handler) — an earlier grep for a FluentValidation RuleFor pattern missed this
/// entirely and wrongly reported it as an unguarded gap. ClampPaginationActionFilter is kept
/// anyway as an outer, uniform defense-in-depth boundary (100) so any *future* endpoint that
/// forgets its own clamp is still protected — it doesn't conflict with a handler's own tighter
/// limit, since whichever layer is stricter wins.
///
/// What this test-writing exercise actually found instead: GET /api/v1/lounges and
/// GET /api/v1/lounges/{id} threw a 500 on every single call (a real, separate, unrelated
/// EF Core/SQLite translation bug, fixed in LoungeRepository — see its comments). Neither endpoint
/// had any test coverage before this session.
/// </summary>
[Collection("Integration")]
public sealed class PaginationClampTests
{
    private readonly ApiFactory _factory;

    public PaginationClampTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetLounges_HugePageSize_IsClampedByHandlersOwnStricterLimit()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/lounges?pageSize=999999999");

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<PaginatedResponse>();
        body!.Data.PageSize.Should().Be(50, "GetLoungesQueryHandler already clamps to 50 internally — " +
            "the filter's looser 100 cap passes it through, then the handler's own limit wins");
    }

    [Fact]
    public async Task GetLounges_ZeroOrNegativePage_IsClampedTo1()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/lounges?page=-5");

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<PaginatedResponse>();
        body!.Data.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetLounges_NormalPageSize_IsUnaffected()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/lounges?pageSize=10");

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<PaginatedResponse>();
        body!.Data.PageSize.Should().Be(10, "normal, in-bounds requests must pass through unchanged");
    }

    // ─── "limit" (suggestions/trending/recommendations — unlike page/pageSize, these return a
    // bare list with no echoed limit/page metadata, so the only externally observable behavior of
    // an unclamped value is a raw 500 from LINQ's Take() rejecting a negative/absurd count) ──────

    [Fact]
    public async Task GetSuggestions_HugeLimit_DoesNotThrow()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/lounge-shows/suggestions?q=test&limit=999999999");

        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetSuggestions_NegativeLimit_DoesNotThrow()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/lounge-shows/suggestions?q=test&limit=-5");

        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetTrending_HugeLimit_DoesNotThrow()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/lounge-shows/trending?limit=999999999");

        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetTrending_NegativeLimit_DoesNotThrow()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/lounge-shows/trending?limit=-5");

        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetRecommendations_HugeLimit_DoesNotThrow()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync("/api/v1/recommendations?limit=999999999");

        res.EnsureSuccessStatusCode();
    }

    private sealed record PaginatedResponse(bool Success, PaginatedData Data);
    private sealed record PaginatedData(int Page, int PageSize, int TotalCount);
}
