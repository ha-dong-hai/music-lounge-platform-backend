using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

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

        var res = await client.GetAsync("/api/v1/complaints/pending");

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
        var body = await createRes.Content.ReadFromJsonAsync<ComplaintCreatedResponse>();

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/complaints/{body!.Data.Id}/resolve",
            new { Status = "Resolved", Resolution = "Checked", ResolvedAction = "Dismiss" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ─── NĐ 147/2024: reactive content takedown on a substantiated complaint ─────

    [Fact]
    public async Task ResolveComplaint_TakeDownContent_CancelsTheShow()
    {
        var showId = await CreateShowAsync(DateTimeOffset.UtcNow.AddDays(20));

        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var createRes = await audienceClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "show",
            TargetId = showId,
            Category = "EventMisrepresentation",
            Description = "This show is running an unlicensed livestream of copyrighted content",
            EvidenceUrls = (string?)null,
            ContactPhone = (string?)null
        });
        var body = await createRes.Content.ReadFromJsonAsync<ComplaintCreatedResponse>();

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/complaints/{body!.Data.Id}/resolve",
            new { Status = "Resolved", Resolution = "Confirmed violation", ResolvedAction = "TakeDownContent" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var showDetail = await adminClient.GetAsync($"/api/v1/lounge-shows/{showId}");
        var showBody = await showDetail.Content.ReadAsStringAsync();
        showBody.Should().Contain("\"status\":\"Cancelled\"");
    }

    [Fact]
    public async Task ResolveComplaint_TakeDownContentOnNonShowTarget_Returns422()
    {
        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var createRes = await audienceClient.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "venue",
            TargetId = SeedHelper.LoungeId,
            Category = "VenueConduct",
            Description = "Complaint against the venue itself, not a specific show",
            EvidenceUrls = (string?)null,
            ContactPhone = (string?)null
        });
        var body = await createRes.Content.ReadFromJsonAsync<ComplaintCreatedResponse>();

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/complaints/{body!.Data.Id}/resolve",
            new { Status = "Resolved", Resolution = "N/A", ResolvedAction = "TakeDownContent" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── D18 Legal approval (NĐ 144/2020 Điều 10) ────────────────────────────

    /// <summary>Một phòng trà riêng cho mỗi lần gọi, một chủ MỚI (MLACP-377: 1 chủ 1 phòng trà), kèm gói
    /// subscription đang chạy của chính chủ đó.</summary>
    private async Task<int> DedicatedVenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        db.OwnerSubscriptions.Add(new OwnerSubscription
        {
            OwnerId = freshOwner.Id, PackageId = 1, StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(29), Status = SubscriptionStatus.Active,
            MaxTicketsPerEventSnapshot = 1000, HasAiPosterSnapshot = true, MaxAiPostersPerMonthSnapshot = 10,
            MaxTourScenesSnapshot = 5
        });
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = freshOwner.Id,
            Name = $"ComplianceVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Compliance", Ward = "P1", District = "Q1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        // Nop duyet buoi dien doi venue phai co san tai khoan nhan tien mac dinh — neu khong,
        // venue ban duoc ve ma khong co cho de nhan tien ve. Venue dung chung cua SeedHelper da
        // co san mot cai; venue rieng nay phai tu dung lay.
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id,
            BankName = "Test Bank",
            AccountNumber = scope.ServiceProvider
                .GetRequiredService<IPiiEncryptionService>().Encrypt("0000009999"),
            AccountHolder = "Test Lounge Owner",
            IsDefault = true, IsVerified = true
        });
        await db.SaveChangesAsync();

        return lounge.Id;
    }

    /// <param name="exactTime">
    /// Bat khi test dang do dung so ngay lam viec truoc buoi dien — luc do moc gio la doi tuong
    /// kiem tra, khong duoc dich di. Cac test con lai chi can "mot buoi dien nao do o tuong lai",
    /// va tu MLACP-308 thi chung phai co khung gio rieng nhau.
    /// </param>
    private async Task<int> CreateShowAsync(DateTimeOffset scheduledStart, bool exactTime = false)
    {
        // Khong dung exactTime thi khong quan tam gio nao, chi can mot gio con trong.
        if (!exactTime) scheduledStart = SeedHelper.NextShowStart();

        // Nhung test do dung "N ngay lam viec truoc buoi dien" thi moc gio la doi tuong kiem tra,
        // khong dich di duoc — ma no lai roi dung vao vung ngay 7-11, la vung dong test khac dang
        // gam san buoi dien o venue dung chung. Nen chung dung mot venue rieng: tu MLACP-308 hai
        // buoi dien chong gio nhau chi xung dot khi o CUNG mot phong tra.
        var loungeId = exactTime
            ? await DedicatedVenueAsync()
            : SeedHelper.LoungeId;

        // MLACP-377: loungeId co the la venue rieng (DedicatedVenueAsync, chu MOI) hoac SeedHelper.LoungeId
        // (chu SeedHelper.OwnerId) — tra dung chu tu DB thay vi gia dinh mot chu co dinh.
        int ownerId;
        using (var ownerScope = _factory.Services.CreateScope())
            ownerId = ownerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Lounges.AsNoTracking().Single(l => l.Id == loungeId).OwnerId;

        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = loungeId,
            Name = $"ComplianceTestShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = "Offline",
            ScheduledStart = scheduledStart,
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 50,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = new[]
            {
                new { PerformerId = (int?)null, PerformerName = "DJ Test", Role = "Main", OrderIndex = 1, SetTime = (string?)null, AcceptsDonation = true }
            }
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    // MLACP-377: showId co the thuoc venue rieng (DedicatedVenueAsync, chu MOI) hoac SeedHelper.LoungeId —
    // tra dung chu/lounge tu show that thay vi gia dinh SeedHelper.LoungeId co dinh.
    private async Task<HttpClient> ClientForShowAsync(int showId)
    {
        int loungeId, ownerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            loungeId = (await db.LoungeShows.AsNoTracking().SingleAsync(s => s.Id == showId)).LoungeId;
            ownerId = (await db.Lounges.AsNoTracking().SingleAsync(l => l.Id == loungeId)).OwnerId;
        }
        return _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);
    }

    private async Task AddTierAsync(int showId)
    {
        var client = await ClientForShowAsync(showId);
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

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Publish_WithReferenceButLessThan7BusinessDays_Returns422()
    {
        var showId = await CreateShowAsync(DateTimeOffset.UtcNow.AddDays(2), exactTime: true);
        await AddTierAsync(showId);
        var client = await ClientForShowAsync(showId);
        await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-TEST-001" });

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

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

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// D18 is enforced with a strict "&lt;" (PublishLoungeShowCommandHandler.cs: businessDaysUntilShow &lt; 7),
    /// so exactly 7 business days must pass and exactly 6 must fail. The two tests above never hit
    /// that line — +2 calendar days and +20 calendar days are both comfortably inside their zone,
    /// not at the boundary itself. This walks the calendar forward counting only Mon-Sat (matching
    /// BusinessDayCalculator's own Sat/Sun exclusion) so the test is deterministic regardless of
    /// which weekday CI happens to run on.
    ///
    /// <para>MLACP-368: walks the VIETNAMESE calendar, like the calculator now does. It used to walk the UTC
    /// calendar — the same assumption as the bug — and would have failed between 00:00 and 07:00 Vietnam
    /// time, when the UTC date is still yesterday.</para>
    /// </summary>
    private static DateTimeOffset DateExactlyNBusinessDaysOut(int n)
    {
        var vn = TimeSpan.FromHours(7);
        var cursor = DateTimeOffset.UtcNow.ToOffset(vn).Date;
        var count = 0;
        while (count < n)
        {
            cursor = cursor.AddDays(1);
            if (cursor.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        }
        // Mid-afternoon Vietnam time so ScheduledStart is unambiguously "on" that business day regardless
        // of the exact time the test happens to run at.
        return new DateTimeOffset(cursor, vn).AddHours(14);
    }

    [Fact]
    public async Task Publish_WithReferenceAndExactly6BusinessDays_Returns422()
    {
        var showId = await CreateShowAsync(DateExactlyNBusinessDaysOut(6), exactTime: true);
        await AddTierAsync(showId);
        var client = await ClientForShowAsync(showId);
        await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-BOUNDARY-6" });

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "6 business days is one short of the >=7 requirement — must still be rejected at the boundary, not just when wildly early");
    }

    [Fact]
    public async Task Publish_WithReferenceAndExactly7BusinessDays_Returns204()
    {
        var showId = await CreateShowAsync(DateExactlyNBusinessDaysOut(7), exactTime: true);
        await AddTierAsync(showId);
        var client = await ClientForShowAsync(showId);
        await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-BOUNDARY-7" });

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "exactly 7 business days is the minimum that must pass — the check is strict '<', not '<='");
    }

    // ─── D19 VCPMC royalty ────────────────────────────────────────────────────

    private async Task<int> CreatePublishedApprovedShowAsync()
    {
        var showId = await CreateShowAsync(DateTimeOffset.UtcNow.AddDays(20));
        await AddTierAsync(showId);
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await ownerClient.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-TEST-003" });
        (await ownerClient.PostAsync($"/api/v1/lounge-shows/{showId}/submit", null)).EnsureSuccessStatusCode();

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

    // POST /complaints tra ve mot object thay vi mot so ke tu MLACP-287: khach vang lai can
    // ma tra cuu de biet ket qua khieu nai cua minh, vi ho khong dang nhap duoc de xem
    // /complaints/my va he thong khong co SMS bao ket qua.
    private sealed record ComplaintCreatedResponse(bool Success, ComplaintCreatedData Data);
    private sealed record ComplaintCreatedData(int Id, string? LookupReference);
}
