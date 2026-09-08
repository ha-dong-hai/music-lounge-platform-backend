using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// CF1 W01/W04/W05/W07 — Venue, Event lifecycle, Staff assignment, Admin approval
/// POST /api/v1/lounges | /api/v1/lounges/{id}/staff
/// POST /api/v1/lounge-shows | .../submit | .../cancel
/// POST /api/v1/ticket-tiers
/// POST /api/v1/moderations/shows/{id}/review
/// </summary>
[Collection("Integration")]
public sealed class EventManagementTests
{
    private readonly ApiFactory _factory;

    public EventManagementTests(ApiFactory factory) => _factory = factory;

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<int> CreateShowAsync(string format = "Offline")
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Show-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = format,
            // D18 (NĐ 144/2020 Điều 10): Publish yêu cầu >=7 ngày làm việc — dùng 14 ngày lịch để
            // luôn đủ dư, không phụ thuộc "hôm nay" là thứ mấy trong tuần.
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(14),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = format == "Online" ? 200 : (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = new[]
            {
                new { PerformerId = (int?)null, PerformerName = "DJ Test", Role = "Main", OrderIndex = 1, SetTime = (string?)null, AcceptsDonation = true }
            }
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<DataResponse<int>>();
        var showId = body!.Data;

        var legalRes = await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval", new
        {
            LegalApprovalReference = "SoVHTT-TEST-0001"
        });
        legalRes.EnsureSuccessStatusCode();

        return showId;
    }

    private async Task CreateTierAsync(int showId, string accessType = "Physical")
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId,
            Name = "Standard",
            Description = (string?)null,
            AccessType = accessType,
            ZoneId = (int?)null,
            TotalCapacity = 100,
            Prices = new[]
            {
                new
                {
                    Name = "Early Bird",
                    Price = 150_000m,
                    Quota = (int?)50,
                    PurchaseChannel = "Both",
                    SaleStart = DateTimeOffset.UtcNow,
                    SaleEnd = DateTimeOffset.UtcNow.AddDays(2)
                }
            }
        });
        res.EnsureSuccessStatusCode();
    }

    // ─── Venue ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateLounge_AsOwner_Returns201()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/lounges", new
        {
            Name = "New Test Lounge",
            Description = "A venue",
            AtmosphereId = (int?)null,
            Street = "1 Test St",
            Ward = "Ward 1",
            District = "District 1",
            City = "HCM",
            Latitude = (double?)10.77,
            Longitude = (double?)106.69
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateLounge_AsAudience_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/lounges", new
        {
            Name = "Should Fail",
            Description = (string?)null,
            AtmosphereId = (int?)null,
            Street = "1 Test St",
            Ward = "Ward 1",
            District = "District 1",
            City = "HCM",
            Latitude = (double?)null,
            Longitude = (double?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // GET /lounges?mine=true predates this session but had zero test coverage — found while
    // auditing the newer LoungeShows/RefundRequests/VenuePenalties "mine" endpoints for the same gap.
    [Fact]
    public async Task GetLounges_MineTrue_AsOwner_IncludesOwnLounge()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var createRes = await client.PostAsJsonAsync("/api/v1/lounges", new
        {
            Name = $"MineTrueLounge-{Guid.NewGuid():N}",
            Description = (string?)null,
            AtmosphereId = (int?)null,
            Street = "1 Test St",
            Ward = "Ward 1",
            District = "District 1",
            City = "HCM",
            Latitude = (double?)null,
            Longitude = (double?)null
        });
        var loungeId = (await createRes.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        var res = await client.GetAsync("/api/v1/lounges?mine=true&pageSize=100");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain($"\"id\":{loungeId}");
    }

    [Fact]
    public async Task GetLounges_MineTrue_AsDifferentOwner_ExcludesOthersLounge()
    {
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var createRes = await ownerClient.PostAsJsonAsync("/api/v1/lounges", new
        {
            Name = $"MineTrueLounge-{Guid.NewGuid():N}",
            Description = (string?)null,
            AtmosphereId = (int?)null,
            Street = "1 Test St",
            Ward = "Ward 1",
            District = "District 1",
            City = "HCM",
            Latitude = (double?)null,
            Longitude = (double?)null
        });
        var loungeId = (await createRes.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");
        var res = await otherOwnerClient.GetAsync("/api/v1/lounges?mine=true&pageSize=100");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().NotContain($"\"id\":{loungeId}", "mine=true must only return the caller's own lounges");
    }

    [Fact]
    public async Task GetLounges_MineTrue_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/lounges?mine=true");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Event lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateLoungeShow_AsOwner_Returns201WithId()
    {
        var id = await CreateShowAsync();
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateLoungeShow_AsAudience_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = "Should fail",
            Description = "x",
            Format = "Offline",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = (int?)null,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Publish_WithoutTicketTiers_Returns422()
    {
        var showId = await CreateShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Publish_OfflineShowWithTier_TransitionsToPending()
    {
        var showId = await CreateShowAsync();
        await CreateTierAsync(showId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await client.GetAsync($"/api/v1/lounge-shows/{showId}");
        var body = await detail.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Pending\"");
    }

    [Fact]
    public async Task ReviewShow_Approve_TransitionsToPublished()
    {
        var showId = await CreateShowAsync();
        await CreateTierAsync(showId);
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await ownerClient.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/moderations/shows/{showId}/review",
            new { Decision = "Approved", ReviewNote = "OK" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await ownerClient.GetAsync($"/api/v1/lounge-shows/{showId}");
        var body = await detail.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Published\"");
    }

    [Fact]
    public async Task ReviewShow_Reject_RevertsToDraft()
    {
        var showId = await CreateShowAsync();
        await CreateTierAsync(showId);
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await ownerClient.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/moderations/shows/{showId}/review",
            new { Decision = "Rejected", ReviewNote = "Missing info" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await ownerClient.GetAsync($"/api/v1/lounge-shows/{showId}");
        var body = await detail.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Draft\"");
    }

    [Fact]
    public async Task ReviewShow_AsNonAdmin_Returns403()
    {
        var showId = await CreateShowAsync();
        await CreateTierAsync(showId);
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await ownerClient.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        var res = await ownerClient.PostAsJsonAsync(
            $"/api/v1/moderations/shows/{showId}/review",
            new { Decision = "Approved", ReviewNote = "OK" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReviewShow_AlreadyDecided_Returns409()
    {
        var showId = await CreateShowAsync();
        await CreateTierAsync(showId);
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await ownerClient.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var first = await adminClient.PostAsJsonAsync(
            $"/api/v1/moderations/shows/{showId}/review",
            new { Decision = "Approved", ReviewNote = "OK" });
        first.EnsureSuccessStatusCode();

        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/moderations/shows/{showId}/review",
            new { Decision = "Approved", ReviewNote = "OK again" });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReviewShow_InvalidDecisionString_Returns400()
    {
        var showId = await CreateShowAsync();
        await CreateTierAsync(showId);
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await ownerClient.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/moderations/shows/{showId}/review",
            new { Decision = "Terminated", ReviewNote = "Not a valid review decision" });

        // FluentValidation rejects anything other than Approved/Rejected before the handler's own
        // Enum.TryParse check ever runs — so this lands as a 400 ValidationException, not the 422
        // DomainException the handler would produce if it were ever reached directly.
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── D15: online show requires a Livestream before publish ───────────────

    [Fact]
    public async Task Publish_OnlineShowWithoutLivestream_Returns422()
    {
        var showId = await CreateShowAsync("Online");
        await CreateTierAsync(showId, "Livestream");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Publish_OnlineShowWithLivestream_Returns204()
    {
        var showId = await CreateShowAsync("Online");
        await CreateTierAsync(showId, "Livestream");

        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        var lsRes = await staffClient.PostAsJsonAsync("/api/v1/livestreams", new { ShowId = showId });
        lsRes.EnsureSuccessStatusCode();

        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await ownerClient.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ─── Cancel ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelLoungeShow_ByOwner_Returns204()
    {
        var showId = await CreateShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CancelLoungeShow_AlreadyCancelled_Returns422()
    {
        var showId = await CreateShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await client.PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null);

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── Staff assignment (W04) ───────────────────────────────────────────────

    [Fact]
    public async Task AssignStaff_ThenDeactivate_RoundTripsCorrectly()
    {
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var assignRes = await ownerClient.PostAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/staff", new { UserId = SeedHelper.OtherOwnerId });
        assignRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var assignBody = await assignRes.Content.ReadFromJsonAsync<DataResponse<int>>();

        var listRes = await ownerClient.GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/staff");
        (await listRes.Content.ReadAsStringAsync()).Should().Contain("\"isActive\":true");

        var deactivateRes = await ownerClient.DeleteAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/staff/{assignBody!.Data}");
        deactivateRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignStaff_ByNonOwner_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/staff", new { UserId = SeedHelper.AudienceId });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLoungeStaff_ByOwnerOfDifferentVenue_Returns403()
    {
        // IDOR/BOLA: chu venue khac (khong so huu SeedHelper.LoungeId) khong duoc xem danh sach
        // staff (ten + email) cua venue nay chi bang cach doan/biet loungeId.
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/staff");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignStaff_UserAlreadyActiveAtAnotherVenue_Returns409()
    {
        // Moi venue phai dung 1 tai khoan staff rieng — cung 1 tai khoan khong duoc active staff
        // o 2 venue cung luc, du 2 venue thuoc 2 Owner khac nhau. Dung user + lounge thu 2 tao rieng
        // cho test nay (khong dung SeedHelper.AudienceId/LoungeId dung chung) de khong de lai active
        // assignment lam anh huong cac test khac dang dung chung 1 database instance.
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        int candidateUserId;
        int secondLoungeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var candidate = new User
            {
                Email = $"multi-venue-{Guid.NewGuid():N}@test.com",
                FullName = "Multi Venue Candidate",
                Role = UserRole.Audience
            };
            db.Users.Add(candidate);

            var secondLounge = new MusicLoungeVenue
            {
                OwnerId = SeedHelper.OtherOwnerId,
                Name = "Second Venue",
                Address = new VenueAddress { Street = "456 Side St", District = "3", City = "HCM" }
            };
            db.Lounges.Add(secondLounge);

            await db.SaveChangesAsync();
            candidateUserId = candidate.Id;
            secondLoungeId = secondLounge.Id;
        }

        var firstAssign = await ownerClient.PostAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/staff", new { UserId = candidateUserId });
        firstAssign.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondAssign = await otherOwnerClient.PostAsJsonAsync(
            $"/api/v1/lounges/{secondLoungeId}/staff", new { UserId = candidateUserId });
        secondAssign.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AssignStaff_ThenDeactivate_RevertsUserRoleBackToAudience()
    {
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var assignRes = await ownerClient.PostAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/staff", new { UserId = SeedHelper.AudienceId });
        assignRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var assignBody = await assignRes.Content.ReadFromJsonAsync<DataResponse<int>>();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var promoted = await db.Users.SingleAsync(u => u.Id == SeedHelper.AudienceId);
            promoted.Role.Should().Be(UserRole.Staff);
        }

        var deactivateRes = await ownerClient.DeleteAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/staff/{assignBody!.Data}");
        deactivateRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reverted = await db.Users.SingleAsync(u => u.Id == SeedHelper.AudienceId);
            reverted.Role.Should().Be(UserRole.Audience);
        }
    }

    [Fact]
    public async Task LookupUserByEmail_ExistingEmail_Returns200()
    {
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await ownerClient.GetAsync($"/api/v1/lounges/staff/lookup?email=audience@test.com");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<DataResponse<UserLookupData>>();
        body!.Data.Id.Should().Be(SeedHelper.AudienceId);
    }

    [Fact]
    public async Task LookupUserByEmail_NonExistentEmail_Returns404WithStandardShape()
    {
        // Previously a hand-rolled `NotFound(ApiResponse<object>.Fail(...))` in the controller —
        // {success,data:null,message}, missing the `errors` field every other 404 in this API has
        // via GlobalExceptionHandler/NotFoundException ({success,message,errors}). Moved the throw
        // into the handler so this endpoint's error shape matches everything else.
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await ownerClient.GetAsync($"/api/v1/lounges/staff/lookup?email=no-such-user@test.com");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out _).Should().BeTrue(
            "the response shape must match every other NotFoundException-driven 404 in this API");
    }

    private sealed record UserLookupData(int Id, string FullName, string Email);

    // ─── GET /lounge-shows?mine=true ────────────────────────────────────────
    // Owner self-service lookup for Draft shows added during REST-standards review — the only
    // path to a Draft show's id was previously the create response (or raw SQL), since every
    // list/search endpoint hard-filters out Draft with no owner exception.

    [Fact]
    public async Task GetPublished_MineTrue_AsOwner_IncludesOwnDraftShow()
    {
        var showId = await CreateShowAsync(); // stays Draft — never published in this helper
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var publicRes = await ownerClient.GetAsync("/api/v1/lounge-shows?pageSize=100");
        (await publicRes.Content.ReadAsStringAsync())
            .Should().NotContain($"\"id\":{showId}", "Draft show must not appear on the default public listing");

        var mineRes = await ownerClient.GetAsync("/api/v1/lounge-shows?mine=true&pageSize=100");

        mineRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await mineRes.Content.ReadAsStringAsync();
        body.Should().Contain($"\"id\":{showId}");
        body.Should().Contain("\"status\":\"Draft\"");
    }

    [Fact]
    public async Task GetPublished_MineTrue_AsDifferentOwner_ExcludesOthersDraftShow()
    {
        var showId = await CreateShowAsync();
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await otherOwnerClient.GetAsync("/api/v1/lounge-shows?mine=true&pageSize=100");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().NotContain($"\"id\":{showId}", "mine=true must only return the caller's own shows");
    }

    [Fact]
    public async Task GetPublished_MineTrue_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/lounge-shows?mine=true");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── B1 authorization gap (MLACP-252, audit-flagged 2026-09-04) ────────────
    // Regression tests: several handlers used to check only "lounge.OwnerId == currentUser.UserId"
    // with no Admin fallback, so an Admin — who passes the controller's
    // [Authorize(Policy = RequireOwner)] gate, which permits Admin by definition — was incorrectly
    // 403'd on a show/tier they don't own despite the policy nominally allowing them through.

    [Fact]
    public async Task UpdateLoungeShow_ByAdmin_NotTheOwner_Returns204()
    {
        var showId = await CreateShowAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}", new
        {
            Name = "Updated by Admin",
            Description = "Updated",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(14),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "Admin must be able to manage any venue's show, matching the controller's declared RequireOwner policy");
    }

    [Fact]
    public async Task CreateTicketTier_ByAdmin_NotTheOwner_Returns201()
    {
        var showId = await CreateShowAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId,
            Name = "Admin-created tier",
            Description = (string?)null,
            AccessType = "Physical",
            ZoneId = (int?)null,
            TotalCapacity = 50,
            Prices = new[]
            {
                new
                {
                    Name = "Standard",
                    Price = 100_000m,
                    Quota = (int?)50,
                    PurchaseChannel = "Both",
                    SaleStart = DateTimeOffset.UtcNow,
                    SaleEnd = DateTimeOffset.UtcNow.AddDays(2)
                }
            }
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created,
            "Admin must be able to manage any venue's ticket tiers, matching the controller's declared RequireOwner policy");
    }

    // ─── C3 capacity race (MLACP-271, audit-flagged 2026-09-07) ────────────────
    // Create/Update both re-tally TotalCapacity across all tiers of a show and compare against the
    // Owner's subscription cap (seed cap is 1000, see SeedHelper), but did so with no lock — 2
    // concurrent requests on different tiers could each read the total before the other's write
    // landed, both pass their own check, and together exceed the cap. Fixed with the same
    // IAsyncKeyedLock pattern used everywhere else in this codebase for the same class of
    // check-then-act race (CancelLoungeShow, Livestream Create, ProcessRefundRequest...).
    //
    // Verified this test's assertions hold with the fix and (via `git stash`) fail without it —
    // BUT: confirmed empirically (by temporarily disabling the lock on ProcessRefundRequestCommand-
    // Handler, an already-established "TwoConcurrent..." test elsewhere in this suite) that this
    // HttpClient+TestServer harness does not reliably force genuine interleaving even at 10-way
    // concurrency — requests appear to execute effectively sequentially, so the loser's own
    // check-time total already reflects the winner's committed write. This test therefore mainly
    // proves the cap-recompute logic is correct on every individual request, the same real
    // guarantee every other "TwoConcurrent..." test in this suite actually provides despite its
    // "concurrent" framing. The lock's necessity under REAL concurrent load is established by code
    // inspection (a genuine read-then-write TOCTOU gap) and by matching an already-proven pattern,
    // not by this test forcing an actual race.

    private async Task<int> CreateTierAsync(int showId, int totalCapacity)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId,
            Name = $"Tier-{Guid.NewGuid():N}",
            Description = (string?)null,
            AccessType = "Physical",
            ZoneId = (int?)null,
            TotalCapacity = totalCapacity,
            Prices = new[]
            {
                new
                {
                    Name = "Standard",
                    Price = 100_000m,
                    Quota = (int?)totalCapacity,
                    PurchaseChannel = "Both",
                    SaleStart = DateTimeOffset.UtcNow,
                    SaleEnd = DateTimeOffset.UtcNow.AddDays(2)
                }
            }
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<DataResponse<int>>();
        return body!.Data;
    }

    [Fact]
    public async Task UpdateTicketTier_TenConcurrentCapacityRaises_NeverExceedsSubscriptionCap()
    {
        var showId = await CreateShowAsync();
        // 10 tiers × 10 = 100, safely under the seed subscription cap of 1000 (SeedHelper).
        const int tierCount = 10;
        var tierIds = new List<int>();
        for (var i = 0; i < tierCount; i++)
            tierIds.Add(await CreateTierAsync(showId, totalCapacity: 10));

        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        // Each tier raises from 10 to 190. Read alone (against the other 9 tiers' still-10 value),
        // every single one of the 10 individually computes 9*10+190=280, well under the 1000 cap —
        // so if genuinely raced, ALL 10 could pass their own check and commit, landing an actual
        // total of 10*190=1900, almost double the cap. Processed correctly one at a time (locked),
        // the arithmetic caps out at exactly 5 successes (100 + 5*180 = 1000, the 6th would push it
        // to 1180) — that count is invariant to processing order since every tier uses the identical
        // 10→190 raise, so this asserts the exact number as a tight regression signal, not just an
        // upper bound.
        var responses = await Task.WhenAll(tierIds.Select(tierId =>
            client.PutAsJsonAsync($"/api/v1/ticket-tiers/{tierId}",
                new { Name = "Raised tier", Description = (string?)null, TotalCapacity = 190 })));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var totalCapacity = await db.TicketTiers
            .Where(t => t.LoungeShowId == showId)
            .SumAsync(t => t.TotalCapacity ?? 0);

        totalCapacity.Should().BeLessThanOrEqualTo(1000,
            "tổng capacity thực tế sau cùng không được vượt hạn mức gói subscription dù có tranh chấp đồng thời — " +
            $"đã ghi nhận {totalCapacity} ({responses.Count(r => r.StatusCode == HttpStatusCode.NoContent)}/{tierCount} request thành công)");
        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(5,
            "đúng 5/10 request được vượt qua kiểm tra hạn mức khi xử lý tuần tự đúng (khóa hoạt động) — " +
            "số lượng thành công lớn hơn nghĩa là có ít nhất 2 request đọc dữ liệu cũ trước khi request kia ghi xong");
        responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).Should().Be(5,
            "5 request còn lại phải thấy đúng lỗi vượt hạn mức, không phải lỗi hạ tầng (500)");
    }

    // ─── B1 authorization gap round 2 (MLACP-256, audit-flagged 2026-09-06) ───
    // Same class of bug as MLACP-252 above, found in 3 more handlers the earlier sweep's grep
    // missed: no Admin fallback at all (not even the older string-literal "Admin" style).

    [Fact]
    public async Task SetLegalApprovalReference_ByAdmin_NotTheOwner_Returns204()
    {
        var showId = await CreateShowAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval", new
        {
            LegalApprovalReference = "SoVHTT-ADMIN-0001"
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "Admin must be able to correct legal-approval data on any venue's show");
    }

    [Fact]
    public async Task SetVcpmcRoyaltyReference_ByAdmin_NotTheOwner_Returns204()
    {
        var showId = await CreateShowAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/vcpmc-royalty", new
        {
            VcpmcRoyaltyReference = "VCPMC-ADMIN-0001"
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "Admin must be able to correct VCPMC-royalty data on any venue's show");
    }

    [Fact]
    public async Task GetPosterGenerationHistory_ByAdmin_NotTheOwner_Returns200()
    {
        var showId = await CreateShowAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.GetAsync($"/api/v1/lounge-shows/{showId}/ai-poster/history");

        res.StatusCode.Should().Be(HttpStatusCode.OK,
            "Admin must be able to review any venue's AI-poster generation history for support/moderation");
    }

    private sealed record DataResponse<T>(bool Success, T Data);
}
