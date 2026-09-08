using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// MLACP-285. All four cap-enforcement sites read `if (activeSub is not null) { check }`, so a venue
/// with no subscription had no cap at all — and PublishLoungeShow does not require a subscription
/// either. Never buying a plan was the way to sell without limit, while the same codebase fails
/// CLOSED for the AI poster feature.
///
/// The fix is not "refuse when there is no plan": that punishes the audience trying to buy a ticket
/// for a show already published, not the venue. Instead the cap always exists and only its source
/// changes — the plan's snapshot, or an explicit free-tier number.
/// </summary>
[Collection("Integration")]
public sealed class FreeTierTicketCapTests
{
    private readonly ApiFactory _factory;

    public FreeTierTicketCapTests(ApiFactory factory) => _factory = factory;

    /// <summary>An owner with NO subscription at all, plus a published show ready to sell.</summary>
    private async Task<(int OwnerId, int ShowId, int PriceId)> SeedUnsubscribedVenueAsync(int quota)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User
        {
            Email = $"nosub-{Guid.NewGuid():N}@test.com",
            FullName = "Owner Chưa Đăng Ký Gói",
            Role = UserRole.Owner,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        (await db.OwnerSubscriptions.AnyAsync(s => s.OwnerId == owner.Id)).Should().BeFalse(
            "test premise: this owner has never bought a plan");

        var lounge = new MusicLounge.Domain.Entities.MusicLounge
        {
            OwnerId = owner.Id,
            Name = $"NoSubVenue-{Guid.NewGuid():N}",
            Description = "Venue chưa đăng ký gói subscription nào",
            Status = LoungeStatus.Approved
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var show = new LoungeShow
        {
            LoungeId = lounge.Id,
            Name = $"NoSubShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(5),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(5).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Standard",
            AccessType = AccessType.Physical
            // TotalCapacity deliberately left null — that is the loophole the cap has to cover.
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Regular", Price = 100_000m, Quota = quota, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(4),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();

        return (owner.Id, show.Id, price.Id);
    }

    [Fact]
    public async Task VenueWithNoSubscription_IsCappedAtTheFreeTierLimitInsteadOfUnlimited()
    {
        var cap = SubscriptionEntitlements.DefaultFreeTierMaxTicketsPerEvent;
        // Quota deliberately far above the free-tier cap: without the fix nothing would stop this.
        var (_, _, priceId) = await SeedUnsubscribedVenueAsync(quota: cap * 10);

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        // HoldTicket caps a single request at 10 tickets (ticket_hold_max_quantity), so fill the
        // free tier in batches rather than one oversized request — the limit under test is the
        // per-show total, not the per-request one.
        const int perHold = 10;
        for (var held = 0; held < cap; held += perHold)
        {
            var batch = await client.PostAsJsonAsync("/api/v1/tickets/holds",
                new { PriceId = priceId, Quantity = perHold });
            batch.StatusCode.Should().Be(HttpStatusCode.Created,
                "the free tier is a limit, not a block — selling up to it must still work " +
                $"(failed after {held} tickets)");
        }

        var overCap = await client.PostAsJsonAsync("/api/v1/tickets/holds",
            new { PriceId = priceId, Quantity = 1 });
        overCap.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "one more ticket crosses the free-tier cap");

        var body = await overCap.Content.ReadAsStringAsync();
        body.Should().Contain("chưa đăng ký gói",
            "the message must say WHICH limit was hit — a free-tier venue and a plan-holding venue " +
            "need completely different next steps");
    }

    [Fact]
    public async Task VenueWithAnActivePlan_StillUsesItsOwnPlanLimit()
    {
        // The seeded Owner holds a plan with MaxTicketsPerEventSnapshot = 1000, far above the free
        // tier — proving the resolver reads the plan when there is one rather than always capping low.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var plan = await db.OwnerSubscriptions.SingleAsync(
            s => s.OwnerId == SeedHelper.OwnerId && s.Status == SubscriptionStatus.Active);

        plan.MaxTicketsPerEventSnapshot.Should()
            .BeGreaterThan(SubscriptionEntitlements.DefaultFreeTierMaxTicketsPerEvent);

        var resolved = SubscriptionEntitlements.ResolveTicketCap(
            SubscriptionEntitlements.ActivePlan([plan], DateTimeOffset.UtcNow),
            SubscriptionEntitlements.DefaultFreeTierMaxTicketsPerEvent);

        resolved.MaxTicketsPerEvent.Should().Be(plan.MaxTicketsPerEventSnapshot);
        resolved.FromActivePlan.Should().BeTrue();
    }

    [Fact]
    public void ExpiredPlan_FallsBackToFreeTierRatherThanKeepingItsOldLimit()
    {
        // A subscription row can sit at Active past its expiry until ExpireSubscriptionsJob runs,
        // so the date check is what actually decides — not the status column alone.
        var expired = new OwnerSubscription
        {
            OwnerId = 999,
            Status = SubscriptionStatus.Active,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-40),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            MaxTicketsPerEventSnapshot = 5000
        };

        var resolved = SubscriptionEntitlements.ResolveTicketCap(
            SubscriptionEntitlements.ActivePlan([expired], DateTimeOffset.UtcNow),
            SubscriptionEntitlements.DefaultFreeTierMaxTicketsPerEvent);

        resolved.MaxTicketsPerEvent.Should().Be(SubscriptionEntitlements.DefaultFreeTierMaxTicketsPerEvent);
        resolved.FromActivePlan.Should().BeFalse();
    }
}
