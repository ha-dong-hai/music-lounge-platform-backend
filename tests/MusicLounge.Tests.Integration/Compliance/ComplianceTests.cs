using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// W30 (NĐ 85/2021 khiếu nại), D18 (NĐ 144/2020 văn bản chấp thuận), D19 (VCPMC tác quyền)
/// POST /api/v1/complaints | /api/v1/admin/complaints/{id}/resolve
/// PUT  /api/v1/lounge-shows/{id}/legal-approval | .../vcpmc-royalty
/// </summary>
[Collection("Integration")]
public sealed class ComplianceTests
{
    private readonly ApiFactory _factory;

    public ComplianceTests(ApiFactory factory) => _factory = factory;

    // ─── W30 Complaints ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateComplaint_AsGuestWithPhone_Returns201()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "venue",
            TargetId = SeedHelper.LoungeId,
            Category = "VenueConduct",
            Description = "Guest complaint",
            EvidenceUrls = (string?)null,
            ContactPhone = "0911111111"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateComplaint_AsGuestWithoutPhone_Returns422()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "venue",
            TargetId = SeedHelper.LoungeId,
            Category = "VenueConduct",
            Description = "Guest complaint no phone",
            EvidenceUrls = (string?)null,
            ContactPhone = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateComplaint_NonExistentTarget_Returns400()
    {
        // AllowAnonymous — TargetId used to only be checked for > 0, so anyone (no login required)
        // could point a complaint at a show/venue/donation/penalty that doesn't exist, leaving
        // Admin with an unresolvable dangling reference.
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "venue",
            TargetId = 999_999_999,
            Category = "VenueConduct",
            Description = "Complaint against a venue that doesn't exist",
            EvidenceUrls = (string?)null,
            ContactPhone = "0911111111"
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateComplaint_AsAuthenticatedUser_ThenGetMy_ReturnsOwnOnly()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var createRes = await client.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "show",
            TargetId = SeedHelper.ShowId,
            Category = "EventMisrepresentation",
            Description = "Authenticated complaint",
            EvidenceUrls = (string?)null,
            ContactPhone = (string?)null
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var getRes = await client.GetAsync("/api/v1/complaints/my");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getRes.Content.ReadAsStringAsync();
        body.Should().Contain("Authenticated complaint");
    }

    [Fact]
    public async Task GetPendingComplaints_AsNonAdmin_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync("/api/v1/admin/complaints");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResolveComplaint_AsAdmin_Returns204()
    {
        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var createRes = await audienceClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "show",
            TargetId = SeedHelper.ShowId,
            Category = "EventMisrepresentation",
            Description = "To be resolved",
            EvidenceUrls = (string?)null,
            ContactPhone = (string?)null
        });
        var body = await createRes.Content.ReadFromJsonAsync<IdResponse>();

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/complaints/{body!.Data}/resolve",
            new { Status = "Resolved", Resolution = "Checked", ResolvedAction = "Dismiss" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ─── D18 Legal approval (NĐ 144/2020 Điều 10) ────────────────────────────

    private async Task<int> CreateShowAsync(DateTimeOffset scheduledStart)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"ComplianceTestShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = "Offline",
            ScheduledStart = scheduledStart,
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 50,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    private async Task AddTierAsync(int showId)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId,
            Name = "Standard",
            Description = (string?)null,
            AccessType = "Physical",
            ZoneId = (int?)null,
            TotalCapacity = 30,
            Prices = new[]
            {
                new
                {
                    Name = "Standard", Price = 100_000m, Quota = (int?)30, PurchaseChannel = "Online",
                    SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(30)
                }
            }
        });
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Publish_WithoutLegalApprovalReference_Returns422()
    {
        var showId = await CreateShowAsync(DateTimeOffset.UtcNow.AddDays(20));
        await AddTierAsync(showId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/publish", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Publish_WithReferenceButLessThan7BusinessDays_Returns422()
    {
        var showId = await CreateShowAsync(DateTimeOffset.UtcNow.AddDays(2));
        await AddTierAsync(showId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-TEST-001" });

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/publish", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Publish_WithReferenceAnd7PlusBusinessDays_Returns204()
    {
        var showId = await CreateShowAsync(DateTimeOffset.UtcNow.AddDays(20));
        await AddTierAsync(showId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-TEST-002" });

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/publish", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ─── D19 VCPMC royalty ────────────────────────────────────────────────────

    private async Task<int> CreatePublishedApprovedShowAsync()
    {
        var showId = await CreateShowAsync(DateTimeOffset.UtcNow.AddDays(20));
        await AddTierAsync(showId);
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await ownerClient.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-TEST-003" });
        (await ownerClient.PostAsync($"/api/v1/lounge-shows/{showId}/publish", null)).EnsureSuccessStatusCode();

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/moderations/shows/{showId}/review",
            new { Decision = "Approved", ReviewNote = "OK" });

        return showId;
    }

    [Fact]
    public async Task StartLoungeShow_WithoutVcpmcReference_Returns422()
    {
        var showId = await CreatePublishedApprovedShowAsync();
        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await staffClient.PostAsync($"/api/v1/lounge-shows/{showId}/start", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task StartLoungeShow_WithVcpmcReference_Returns204()
    {
        var showId = await CreatePublishedApprovedShowAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await ownerClient.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/vcpmc-royalty",
            new { VcpmcRoyaltyReference = "VCPMC-HD-TEST-001" });

        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        var res = await staffClient.PostAsync($"/api/v1/lounge-shows/{showId}/start", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record IdResponse(bool Success, int Data);
}
