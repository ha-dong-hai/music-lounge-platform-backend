using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// CF4 W07/W09/W22 — Livestream lifecycle
/// POST /api/v1/livestreams
/// POST /api/v1/livestreams/{id}/start
/// POST /api/v1/livestreams/{id}/end
/// GET  /api/v1/livestreams/{id}
/// POST /api/v1/livestreams/{id}/terminate
/// </summary>
[Collection("Integration")]
public sealed class LivestreamTests
{
    private readonly ApiFactory _factory;

    public LivestreamTests(ApiFactory factory) => _factory = factory;

    // ─── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a fresh show in the DB so each test gets its own show (no ConflictException).</summary>
    private async Task<int> CreateFreshShowAsync()
    {
        int showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = new MusicLounge.Domain.Entities.LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"TestShow-{Guid.NewGuid():N}",
                Description = "Integration test show",
                Format = MusicLounge.Domain.Enums.LoungeShowFormat.Online,
                Status = MusicLounge.Domain.Enums.LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                VcpmcRoyaltyReference = "TEST-VCPMC-REF"
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            showId = show.Id;
        }
        return showId;
    }

    private async Task<int> CreateAndApproveLivestreamAsync()
    {
        var showId = await CreateFreshShowAsync();
        return await CreateAndApproveLivestreamForShowAsync(showId);
    }

    /// <summary>
    /// Creates a show, a Livestream TicketTier, a confirmed ticket for AudienceId,
    /// then creates and approves a livestream for that show.
    /// Used by HLS access-control tests where the audience must have a valid ticket.
    /// </summary>
    private async Task<int> CreateAndApproveLivestreamWithAudienceTicketAsync()
    {
        int showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var show = new MusicLounge.Domain.Entities.LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"TestShow-{Guid.NewGuid():N}",
                Description = "Integration test show",
                Format = LoungeShowFormat.Online,
                Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            showId = show.Id;

            var tier = new MusicLounge.Domain.Entities.TicketTier
            {
                LoungeShowId = showId,
                Name = "Online",
                AccessType = AccessType.Livestream,
                CreatedAt = DateTime.UtcNow
            };
            db.Add(tier);
            await db.SaveChangesAsync();

            // PriceId reuses the seeded TicketPrice — FK not enforced in SQLite
            db.Add(new MusicLounge.Domain.Entities.Ticket
            {
                Id = Guid.NewGuid(),
                BuyerId = SeedHelper.AudienceId,
                PriceId = SeedHelper.TicketPriceId,
                TierId = tier.Id,
                ShowId = showId,
                Status = TicketStatus.Confirmed,
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return await CreateAndApproveLivestreamForShowAsync(showId);
    }

    private async Task<int> CreateAndApproveLivestreamForShowAsync(int showId)
    {
        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        var createRes = await staffClient.PostAsJsonAsync("/api/v1/livestreams", new { ShowId = showId });
        createRes.EnsureSuccessStatusCode();
        var body = await createRes.Content.ReadFromJsonAsync<IdResponse>();
        var id = body!.Data;

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/moderations/livestreams/{id}/review",
            new { Decision = "Approved", ReviewNote = "OK" });

        return id;
    }

    private async Task<int> CreateApprovedAndStartedLivestreamAsync()
    {
        var id = await CreateAndApproveLivestreamAsync();
        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        (await staffClient.PostAsync($"/api/v1/livestreams/{id}/start", null)).EnsureSuccessStatusCode();
        return id;
    }

    // ─── Create tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateLivestream_AsStaff_Returns201WithId()
    {
        var showId = await CreateFreshShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/livestreams",
            new { ShowId = showId });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        body!.Data.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateLivestream_AsAudience_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/livestreams",
            new { ShowId = SeedHelper.ShowId });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateLivestream_ForOfflineShow_Returns422()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/livestreams",
            new { ShowId = SeedHelper.OfflineShowId });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateLivestream_ForCancelledShow_Returns422()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/livestreams",
            new { ShowId = SeedHelper.CancelledShowId });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── Start/End tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task StartLivestream_BeforeAdminApproval_Returns422()
    {
        var showId = await CreateFreshShowAsync();
        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        var createRes = await staffClient.PostAsJsonAsync("/api/v1/livestreams",
            new { ShowId = showId });
        var id = (await createRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        // Try to start WITHOUT admin approval
        var startRes = await staffClient.PostAsync($"/api/v1/livestreams/{id}/start", null);

        startRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task StartLivestream_AfterAdminApproval_Returns204()
    {
        var id = await CreateAndApproveLivestreamAsync();
        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var startRes = await staffClient.PostAsync($"/api/v1/livestreams/{id}/start", null);

        startRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task EndLivestream_WhileLive_Returns204()
    {
        var id = await CreateApprovedAndStartedLivestreamAsync();
        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await staffClient.PostAsync($"/api/v1/livestreams/{id}/end", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ─── GetDetail / HLS URL access control ───────────────────────────────────

    [Fact]
    public async Task GetDetail_AsAdmin_ReturnsHlsUrl()
    {
        var id = await CreateAndApproveLivestreamAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.GetAsync($"/api/v1/livestreams/{id}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("data").GetProperty("hlsUrl").GetString()
            .Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetDetail_AsAudienceWithTicket_ReturnsHlsUrl()
    {
        var id = await CreateAndApproveLivestreamWithAudienceTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync($"/api/v1/livestreams/{id}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("data").GetProperty("hlsUrl").GetString()
            .Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetDetail_AsAudienceWithoutTicket_HlsUrlIsNull()
    {
        var id = await CreateAndApproveLivestreamAsync();
        // OtherOwnerId has no ticket for this show
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.GetAsync($"/api/v1/livestreams/{id}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var hlsUrl = json.RootElement.GetProperty("data").GetProperty("hlsUrl");
        (hlsUrl.ValueKind == JsonValueKind.Null || hlsUrl.GetString() is null)
            .Should().BeTrue("HLS URL must be hidden from non-ticket-holders");
    }

    [Fact]
    public async Task GetDetail_Unauthenticated_Returns401()
    {
        var id = await CreateAndApproveLivestreamAsync();
        var client = _factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/livestreams/{id}");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Terminate tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task TerminateLivestream_AsAdmin_Returns204()
    {
        var id = await CreateApprovedAndStartedLivestreamAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PostAsJsonAsync($"/api/v1/livestreams/{id}/terminate",
            new { Reason = "Policy violation" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TerminateLivestream_WithEmptyReason_Returns400()
    {
        var id = await CreateApprovedAndStartedLivestreamAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PostAsJsonAsync($"/api/v1/livestreams/{id}/terminate",
            new { Reason = "" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TerminateLivestream_WhenNotLive_Returns422()
    {
        var id = await CreateAndApproveLivestreamAsync(); // Scheduled, not Live
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PostAsJsonAsync($"/api/v1/livestreams/{id}/terminate",
            new { Reason = "Admin force stop" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── Credentials (RTMP/StreamKey) — previously zero test coverage ────────

    [Fact]
    public async Task GetCredentials_AsStaff_Returns200WithRtmpAndKey()
    {
        var id = await CreateAndApproveLivestreamAsync();
        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await staffClient.GetAsync($"/api/v1/livestreams/{id}/credentials");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("rtmpUrl").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("streamKey").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetCredentials_AsAudience_Returns403()
    {
        var id = await CreateAndApproveLivestreamAsync();
        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await audienceClient.GetAsync($"/api/v1/livestreams/{id}/credentials");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "RTMP URL/StreamKey must never leak to non-staff — this is the only barrier against hijacking the stream");
    }

    // ─── Chat history — previously zero test coverage. Sending only happens via
    //     SignalR (see ChatRateLimiterTests), so a message is seeded directly here. ───

    [Fact]
    public async Task GetChatHistory_AsTicketHolder_Returns200WithMessages()
    {
        var id = await CreateAndApproveLivestreamWithAudienceTicketAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new MusicLounge.Domain.Entities.LivestreamChatMessage
            {
                LivestreamId = id, UserId = SeedHelper.AudienceId,
                Message = "Hello from the golden path!", SentAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await audienceClient.GetAsync($"/api/v1/livestreams/{id}/chat");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Hello from the golden path!");
    }

    [Fact]
    public async Task GetChatHistory_WithoutTicket_Returns403()
    {
        var id = await CreateAndApproveLivestreamAsync(); // no audience ticket for this show
        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await audienceClient.GetAsync($"/api/v1/livestreams/{id}/chat");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Concurrent-session limit (LivestreamViewingSession + heartbeat) ─────────────────────────
    // Mux HLS công khai, không DRM — hạn mức chỉ chặn PHIÊN MỚI vượt quá, không ép ngắt phiên cũ
    // đang mở. Mặc định (không seed system_config, dùng fallback): 2 phiên/vé, timeout 90s.

    [Fact]
    public async Task GetDetail_AsAudienceWithTicket_ReturnsNonNullViewingSessionId()
    {
        var id = await CreateAndApproveLivestreamWithAudienceTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync($"/api/v1/livestreams/{id}");

        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("data").GetProperty("viewingSessionId").GetString()
            .Should().NotBeNullOrEmpty("a genuine ticket holder must get a session id to heartbeat with");
    }

    [Fact]
    public async Task GetDetail_AsAdmin_ViewingSessionIdIsNull()
    {
        var id = await CreateAndApproveLivestreamAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.GetAsync($"/api/v1/livestreams/{id}");

        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var value = json.RootElement.GetProperty("data").GetProperty("viewingSessionId");
        (value.ValueKind == JsonValueKind.Null).Should().BeTrue(
            "Admin monitoring a stream isn't subject to the per-ticket concurrent-session limit");
    }

    [Fact]
    public async Task GetDetail_ThirdConcurrentSessionForSameTicket_Returns422()
    {
        var id = await CreateAndApproveLivestreamWithAudienceTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res1 = await client.GetAsync($"/api/v1/livestreams/{id}");
        var res2 = await client.GetAsync($"/api/v1/livestreams/{id}");
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        res2.StatusCode.Should().Be(HttpStatusCode.OK, "default max is 2 concurrent sessions per ticket");

        var res3 = await client.GetAsync($"/api/v1/livestreams/{id}");

        res3.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a 3rd device opening HlsUrl while 2 sessions are still within the heartbeat timeout must be blocked");
    }

    [Fact]
    public async Task GetDetail_AfterFirstSessionExpires_AllowsNewSessionEvenAtCap()
    {
        var id = await CreateAndApproveLivestreamWithAudienceTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await client.GetAsync($"/api/v1/livestreams/{id}");
        await client.GetAsync($"/api/v1/livestreams/{id}");

        // Simulate the 1st session's device having gone silent long enough to time out — no
        // cleanup job needed, the cap check itself filters by LastHeartbeatAt (TicketHold.ExpiresAt
        // pattern).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var oldestSession = await db.Set<MusicLounge.Domain.Entities.LivestreamViewingSession>()
                .Where(s => s.LivestreamId == id)
                .OrderBy(s => s.Id).FirstAsync();
            oldestSession.LastHeartbeatAt = DateTimeOffset.UtcNow.AddSeconds(-91);
            await db.SaveChangesAsync();
        }

        var res3 = await client.GetAsync($"/api/v1/livestreams/{id}");

        res3.StatusCode.Should().Be(HttpStatusCode.OK,
            "an expired (no-heartbeat) session must free up a slot without any cleanup job");
    }

    [Fact]
    public async Task Heartbeat_ValidSession_Returns204()
    {
        var id = await CreateAndApproveLivestreamWithAudienceTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var detailRes = await client.GetAsync($"/api/v1/livestreams/{id}");
        var sessionId = JsonDocument.Parse(await detailRes.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("viewingSessionId").GetString();

        var res = await client.PostAsJsonAsync($"/api/v1/livestreams/{id}/heartbeat", new { SessionId = sessionId });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Heartbeat_KeepsSessionAlive_SoItStillCountsTowardTheCap()
    {
        var id = await CreateAndApproveLivestreamWithAudienceTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var detail1 = await client.GetAsync($"/api/v1/livestreams/{id}");
        var session1Id = JsonDocument.Parse(await detail1.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("viewingSessionId").GetString();
        await client.GetAsync($"/api/v1/livestreams/{id}"); // 2nd session, now at cap

        // Heartbeat the 1st session so it's still fresh — must NOT free up a slot the way the
        // expiry test above does.
        await client.PostAsJsonAsync($"/api/v1/livestreams/{id}/heartbeat", new { SessionId = session1Id });

        var res3 = await client.GetAsync($"/api/v1/livestreams/{id}");

        res3.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a heartbeated session is still active and must keep counting toward the cap");
    }

    [Fact]
    public async Task Heartbeat_UnknownSessionId_Returns404()
    {
        var id = await CreateAndApproveLivestreamWithAudienceTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/livestreams/{id}/heartbeat", new { SessionId = Guid.NewGuid().ToString("N") });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Heartbeat_ByNonOwnerOfTheTicket_Returns403()
    {
        var id = await CreateAndApproveLivestreamWithAudienceTicketAsync();
        var ticketHolderClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var detailRes = await ticketHolderClient.GetAsync($"/api/v1/livestreams/{id}");
        var sessionId = JsonDocument.Parse(await detailRes.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("viewingSessionId").GetString();

        // OtherOwnerId did not open this session and holds no ticket for this show.
        var strangerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");
        var res = await strangerClient.PostAsJsonAsync(
            $"/api/v1/livestreams/{id}/heartbeat", new { SessionId = sessionId });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record IdResponse(bool Success, int Data);
}
