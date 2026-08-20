using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF7;

/// <summary>
/// BR-28 — venue penalties (warning/suspension/ban), appeals, and enforcement (§6.8, §6.17).
/// Found entirely unimplemented during this session's audit: schema existed (VenuePenalty had
/// every field needed), but there was no command to issue a penalty, no appeal flow, and nothing
/// anywhere checked Lounge.Status — a "suspended" venue could operate completely normally.
///
/// Every test that mutates Lounge.Status or OwnerSubscription state creates its own fresh
/// owner/lounge/subscription rather than reusing SeedHelper.OwnerId/LoungeId — that shared owner's
/// subscription is relied on by tests in other files (event creation gates), and this session's
/// earlier fixes were repeatedly bitten by cross-test state sharing under the single-collection
/// SQLite database, so isolating state here from the start rather than discovering the collision
/// later.
/// </summary>
[Collection("Integration")]
public sealed class VenuePenaltyTests
{
    private readonly ApiFactory _factory;

    public VenuePenaltyTests(ApiFactory factory) => _factory = factory;

    private async Task<(int OwnerId, int LoungeId, int SubscriptionId)> CreateFreshOwnerLoungeSubscriptionAsync(
        decimal subscriptionPrice = 500_000m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"pen-owner-{Guid.NewGuid():N}@test.com", FullName = "Penalty Test Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"PenaltyTestLounge-{Guid.NewGuid():N}"[..30],
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);

        var package = new SubscriptionPackage
        {
            Name = $"PenaltyTestPkg-{Guid.NewGuid():N}"[..20], Price = subscriptionPrice,
            BillingCycle = SubscriptionBillingCycle.Monthly,
            MaxTicketsPerEvent = 100, HasAiPoster = false, IsActive = true, CreatedAt = DateTimeOffset.UtcNow
        };
        db.SubscriptionPackages.Add(package);
        await db.SaveChangesAsync();

        var subscription = new OwnerSubscription
        {
            OwnerId = owner.Id, PackageId = package.Id,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-15),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(15), // 30-day cycle, exactly half elapsed
            Status = SubscriptionStatus.Active,
            MaxTicketsPerEventSnapshot = 100, HasAiPosterSnapshot = false
        };
        db.OwnerSubscriptions.Add(subscription);
        await db.SaveChangesAsync();

        return (owner.Id, lounge.Id, subscription.Id);
    }

    private async Task<int> SeedPenaltyAsync(
        int loungeId, PenaltyType type, DateTimeOffset effectiveAt,
        PenaltyStatus status = PenaltyStatus.Active, int? suspensionDays = null,
        DateTimeOffset? appealDeadline = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var penalty = new VenuePenalty
        {
            LoungeId = loungeId, PenaltyType = type, Reason = "Test violation",
            IssuedBy = SeedHelper.AdminId, IssuedAt = DateTimeOffset.UtcNow, EffectiveAt = effectiveAt,
            SuspensionDays = suspensionDays, Status = status, AppealDeadline = appealDeadline
        };
        db.VenuePenalties.Add(penalty);
        await db.SaveChangesAsync();
        return penalty.Id;
    }

    // ─── Issue penalty ────────────────────────────────────────────────────────

    [Fact]
    public async Task Issue_AsNonAdmin_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = SeedHelper.LoungeId, PenaltyType = "Warning", Reason = "test", EvidenceRef = (string?)null,
            SuspensionDays = (int?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Issue_SuspensionWithoutDays_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = SeedHelper.LoungeId, PenaltyType = "Suspension", Reason = "test", EvidenceRef = (string?)null,
            SuspensionDays = (int?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Issue_NonExistentLounge_Returns400()
    {
        // IssuePenaltyCommandValidator now checks LoungeId existence up front (master-backend-techlead
        // review — FK existence checks belong in the Validator, not discovered deep in the handler).
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = 999_999_999, PenaltyType = "Warning", Reason = "test", EvidenceRef = (string?)null,
            SuspensionDays = (int?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Issue_Warning_AppliesLoungeStatusImmediately()
    {
        var (_, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = loungeId, PenaltyType = "Warning", Reason = "Minor issue", EvidenceRef = (string?)null,
            SuspensionDays = (int?)null
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = await db.Lounges.SingleAsync(l => l.Id == loungeId);
        lounge.Status.Should().Be(LoungeStatus.Warned, "warning takes effect immediately, no delay");
    }

    [Fact]
    public async Task Issue_Suspension_DoesNotApplyImmediately()
    {
        var (_, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        await client.PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = loungeId, PenaltyType = "Suspension", Reason = "Repeated violation",
            EvidenceRef = (string?)null, SuspensionDays = 5
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = await db.Lounges.SingleAsync(l => l.Id == loungeId);
        lounge.Status.Should().Be(LoungeStatus.Approved, "§6.8 — suspension has a 24h delay before it takes effect");

        var penalty = await db.VenuePenalties.SingleAsync(p => p.LoungeId == loungeId);
        penalty.EffectiveAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
    }

    // ─── ApplyDuePenaltiesJob ─────────────────────────────────────────────────

    [Fact]
    public async Task ApplyDuePenaltiesJob_Suspension_AppliesStatusAndExtendsSubscription()
    {
        var (ownerId, loungeId, subId) = await CreateFreshOwnerLoungeSubscriptionAsync();
        DateTimeOffset originalExpiry;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            originalExpiry = (await db.OwnerSubscriptions.SingleAsync(s => s.Id == subId)).ExpiresAt;
        }
        await SeedPenaltyAsync(loungeId, PenaltyType.Suspension, DateTimeOffset.UtcNow.AddMinutes(-1), suspensionDays: 10);

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ApplyDuePenaltiesJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = await verifyDb.Lounges.SingleAsync(l => l.Id == loungeId);
        lounge.Status.Should().Be(LoungeStatus.Suspended);

        var subscription = await verifyDb.OwnerSubscriptions.SingleAsync(s => s.Id == subId);
        subscription.ExpiresAt.Should().BeCloseTo(originalExpiry.AddDays(10), TimeSpan.FromMinutes(1),
            "§6.8 — suspension days are added back to the owner's subscription as compensation");
    }

    [Fact]
    public async Task ApplyDuePenaltiesJob_Ban_LocksVenueAndRefundsProRataViaLedger()
    {
        var (ownerId, loungeId, subId) = await CreateFreshOwnerLoungeSubscriptionAsync(subscriptionPrice: 300_000m);
        await SeedPenaltyAsync(loungeId, PenaltyType.Ban, DateTimeOffset.UtcNow.AddMinutes(-1));

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ApplyDuePenaltiesJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var db2Scope = _factory.Services.CreateScope();
        var db = db2Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lounge = await db.Lounges.SingleAsync(l => l.Id == loungeId);
        lounge.Status.Should().Be(LoungeStatus.Locked);

        var subscription = await db.OwnerSubscriptions.SingleAsync(s => s.Id == subId);
        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);

        // Seeded with exactly half the 30-day cycle remaining (15/30) — refund should be ~half of 300,000.
        var platformAccount = await db.LedgerAccounts.SingleAsync(a => a.OwnerType == AccountType.Platform && a.OwnerId == null);
        var entries = await db.LedgerEntries
            .Where(e => e.ReferenceType == "subscription" && e.ReferenceId == subId.ToString())
            .ToListAsync();
        entries.Should().NotBeEmpty("ban must reverse subscription revenue through the ledger, not silently");
        entries.Where(e => e.IsDebit).Sum(e => e.Amount).Should().Be(entries.Where(e => !e.IsDebit).Sum(e => e.Amount));

        var platformDebit = entries.Single(e => e.AccountId == platformAccount.Id);
        platformDebit.IsDebit.Should().BeTrue();
        platformDebit.Amount.Should().BeApproximately(150_000m, 5_000m, "≈ half of 300,000đ for ≈half the cycle remaining");
    }

    [Fact]
    public async Task ApplyDuePenaltiesJob_RunTwice_DoesNotDoubleApplyCompensation()
    {
        var (_, loungeId, subId) = await CreateFreshOwnerLoungeSubscriptionAsync();
        DateTimeOffset originalExpiry;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            originalExpiry = (await db.OwnerSubscriptions.SingleAsync(s => s.Id == subId)).ExpiresAt;
        }
        await SeedPenaltyAsync(loungeId, PenaltyType.Suspension, DateTimeOffset.UtcNow.AddMinutes(-1), suspensionDays: 10);

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ApplyDuePenaltiesJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
            await job.ExecuteAsync(new JobCancellationToken(false)); // simulates an overlapping/retried Hangfire run
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subscription = await verifyDb.OwnerSubscriptions.SingleAsync(s => s.Id == subId);
        subscription.ExpiresAt.Should().BeCloseTo(originalExpiry.AddDays(10), TimeSpan.FromMinutes(1),
            "running the job twice must not extend the subscription by 20 days instead of 10");
    }

    // ─── Appeal ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAppeal_ByOwner_SetsAppealedStatusAndDeadline()
    {
        var (ownerId, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        var penaltyId = await SeedPenaltyAsync(loungeId, PenaltyType.Suspension, DateTimeOffset.UtcNow.AddHours(24), suspensionDays: 5);
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);

        var res = await client.PostAsJsonAsync(
            $"/api/v1/venue-penalties/{penaltyId}/appeal", new { AppealReason = "Chúng tôi không vi phạm" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var penalty = await db.VenuePenalties.SingleAsync(p => p.Id == penaltyId);
        penalty.Status.Should().Be(PenaltyStatus.Appealed);
        penalty.AppealedAt.Should().NotBeNull();
        penalty.AppealDeadline.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(48), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SubmitAppeal_ByNonOwner_Returns403()
    {
        var (_, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        var penaltyId = await SeedPenaltyAsync(loungeId, PenaltyType.Warning, DateTimeOffset.UtcNow);
        // A real Owner account, but not the one this lounge belongs to.
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner", loungeId);

        var res = await client.PostAsJsonAsync(
            $"/api/v1/venue-penalties/{penaltyId}/appeal", new { AppealReason = "Not mine to appeal" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReviewAppeal_Overturned_RestoresApprovedStatus()
    {
        var (ownerId, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        // Already applied (EffectiveAt in the past) and appealed, to exercise the "already applied" branch too.
        var penaltyId = await SeedPenaltyAsync(
            loungeId, PenaltyType.Suspension, DateTimeOffset.UtcNow.AddMinutes(-1),
            status: PenaltyStatus.Appealed, suspensionDays: 5, appealDeadline: DateTimeOffset.UtcNow.AddHours(40));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lounge = await db.Lounges.SingleAsync(l => l.Id == loungeId);
            lounge.Status = LoungeStatus.Suspended; // simulate ApplyDuePenaltiesJob having already run
            await db.SaveChangesAsync();
        }

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/venue-penalties/{penaltyId}/appeal/review",
            new { Decision = "Overturned", ReviewNote = "Xác minh không có vi phạm" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge2 = await verifyDb.Lounges.SingleAsync(l => l.Id == loungeId);
        lounge2.Status.Should().Be(LoungeStatus.Approved);

        var penalty = await verifyDb.VenuePenalties.SingleAsync(p => p.Id == penaltyId);
        penalty.Status.Should().Be(PenaltyStatus.Overturned);
        penalty.AppealResult.Should().Be("Overturned");
    }

    [Fact]
    public async Task ReviewAppeal_Upheld_KeepsPenaltyStatusUpheld()
    {
        var (ownerId, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        var penaltyId = await SeedPenaltyAsync(
            loungeId, PenaltyType.Warning, DateTimeOffset.UtcNow, status: PenaltyStatus.Appealed,
            appealDeadline: DateTimeOffset.UtcNow.AddHours(40));

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/venue-penalties/{penaltyId}/appeal/review",
            new { Decision = "Upheld", ReviewNote = "Vi phạm được xác nhận" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var penalty = await db.VenuePenalties.SingleAsync(p => p.Id == penaltyId);
        penalty.Status.Should().Be(PenaltyStatus.Upheld);
    }

    [Fact]
    public async Task AutoApproveOverdueAppealsJob_PastDeadline_AutoOverturnsAndRestoresStatus()
    {
        var (_, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        var penaltyId = await SeedPenaltyAsync(
            loungeId, PenaltyType.Warning, DateTimeOffset.UtcNow, status: PenaltyStatus.Appealed,
            appealDeadline: DateTimeOffset.UtcNow.AddMinutes(-1)); // SLA already missed
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lounge = await db.Lounges.SingleAsync(l => l.Id == loungeId);
            lounge.Status = LoungeStatus.Warned;
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<AutoApproveOverdueAppealsJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var penalty = await verifyDb.VenuePenalties.SingleAsync(p => p.Id == penaltyId);
        penalty.Status.Should().Be(PenaltyStatus.Overturned);
        penalty.AppealResult.Should().Contain("auto");

        var lounge2 = await verifyDb.Lounges.SingleAsync(l => l.Id == loungeId);
        lounge2.Status.Should().Be(LoungeStatus.Approved, "bảo vệ Owner khỏi bị phạt oan khi Admin không xử lý kịp SLA");
    }

    // ─── Enforcement ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishLoungeShow_WhenVenueSuspended_Returns422()
    {
        var (ownerId, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        int showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lounge = await db.Lounges.SingleAsync(l => l.Id == loungeId);
            lounge.Status = LoungeStatus.Suspended;

            var show = new LoungeShow
            {
                LoungeId = loungeId, Name = "Blocked Show", Description = "test",
                Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Draft,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(20),
                LegalApprovalReference = "SoVHTT-TEST-PENALTY"
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();

            db.TicketTiers.Add(new TicketTier
            {
                LoungeShowId = show.Id, Name = "Standard", AccessType = AccessType.Physical, TotalCapacity = 10
            });
            await db.SaveChangesAsync();
            showId = show.Id;
        }

        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);
        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/publish", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── GET /venue-penalties/mine ───────────────────────────────────────────
    // Added during REST-standards review — before this, VenuePenaltiesController had NO read
    // endpoint at all (not even a list), so an owner had no way to see penalties issued against
    // their own venue except contacting support.

    [Fact]
    public async Task GetMine_AsOwner_ContainsOwnPenalty()
    {
        var (ownerId, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        var penaltyId = await SeedPenaltyAsync(loungeId, PenaltyType.Warning, DateTimeOffset.UtcNow);
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);

        var res = await client.GetAsync("/api/v1/venue-penalties/mine");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain($"\"id\":{penaltyId}");
    }

    [Fact]
    public async Task GetMine_AsDifferentOwner_ExcludesOthersPenalty()
    {
        var (_, loungeId, _) = await CreateFreshOwnerLoungeSubscriptionAsync();
        var penaltyId = await SeedPenaltyAsync(loungeId, PenaltyType.Warning, DateTimeOffset.UtcNow);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.GetAsync("/api/v1/venue-penalties/mine");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().NotContain($"\"id\":{penaltyId}", "must only return penalties against the caller's own lounges");
    }

    [Fact]
    public async Task GetMine_AsNonOwner_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync("/api/v1/venue-penalties/mine");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
