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

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-288. MLACP-279 disclosed the refund policy to buyers and, in doing so, recorded that the
/// three D13 columns behind it were written by nothing at all: no endpoint accepted them, no handler
/// set them, so every show on the platform ran on the defaults. The schema, the resolver and the
/// enforcement existed; only the write path was missing.
///
/// These tests pin the two halves that make the write path worth having. First, what the owner
/// chooses is exactly what the buyer is shown and exactly what cancelling grants — the same property
/// <see cref="RefundPolicyDisclosureTests"/> pins for the defaults, now with a policy that is not
/// the default. Second, the owner's freedom stops where the platform's own guarantee begins: a show
/// sold as strictly non-refundable still refunds in full when the venue is the one who cancels.
/// </summary>
[Collection("Integration")]
public sealed class RefundPolicyWritePathTests
{
    private readonly ApiFactory _factory;

    public RefundPolicyWritePathTests(ApiFactory factory) => _factory = factory;

    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

    private static object CreateBody(
        bool? cancellationAllowed = null,
        decimal? refundPercentage = null,
        int? cancellationDeadlineHours = null,
        int daysUntilShow = 30) => new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"PolicyWriteShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = "Offline",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(daysUntilShow),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(daysUntilShow).AddHours(3),
            TicketSaleClosesAt = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = (int?)null,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = Array.Empty<object>(),
            CancellationAllowed = cancellationAllowed,
            RefundPercentage = refundPercentage,
            CancellationDeadlineHours = cancellationDeadlineHours
        };

    private async Task<RefundPolicy> ReadPublishedPolicyAsync(HttpClient client, int showId)
    {
        var res = await client.GetAsync($"/api/v1/lounge-shows/{showId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<Envelope<ShowDetail>>();
        return body!.Data.RefundPolicy!;
    }

    [Fact]
    public async Task PolicyTheOwnerChooses_IsThePolicyTheShowPagePublishes()
    {
        var client = Owner();

        var create = await client.PostAsJsonAsync("/api/v1/lounge-shows",
            CreateBody(cancellationAllowed: true, refundPercentage: 70m, cancellationDeadlineHours: 72));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var showId = (await create.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        var policy = await ReadPublishedPolicyAsync(client, showId);

        policy.CancellationAllowed.Should().BeTrue();
        policy.RefundPercentage.Should().Be(70m, "the owner chose 70, not the platform default 100");
        policy.DeadlineHoursBeforeStart.Should().Be(72);
        policy.CancelBefore.Should().NotBeNull();
        policy.Summary.Should().Contain("70").And.Contain("72",
            "the buyer reads the sentence, not the fields — both numbers have to appear in it");
    }

    [Fact]
    public async Task OmittingThePolicy_LeavesEveryShowOnTheSameDefaultsAsBefore()
    {
        // The write path is additive: a client that has not been updated yet must keep producing
        // exactly the shows it produced before MLACP-288.
        var client = Owner();

        var create = await client.PostAsJsonAsync("/api/v1/lounge-shows", CreateBody());
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var showId = (await create.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        var policy = await ReadPublishedPolicyAsync(client, showId);

        policy.CancellationAllowed.Should().BeTrue();
        policy.RefundPercentage.Should().Be(100m);
        policy.DeadlineHoursBeforeStart.Should().BeNull();
    }

    [Fact]
    public async Task PercentageTheOwnerChose_IsThePercentageCancellingActuallyGrants()
    {
        var client = Owner();

        var create = await client.PostAsJsonAsync("/api/v1/lounge-shows",
            CreateBody(cancellationAllowed: true, refundPercentage: 40m));
        var showId = (await create.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        // Take the show live and give it something to sell. Done directly rather than through
        // publish + moderation because the subject here is the policy, not the approval workflow.
        int priceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = await db.LoungeShows.SingleAsync(s => s.Id == showId);
            show.Status = LoungeShowStatus.Published;

            var tier = new TicketTier
            {
                LoungeShowId = showId, Name = "Standard",
                AccessType = AccessType.Physical, TotalCapacity = 20
            };
            db.Add(tier);
            await db.SaveChangesAsync();

            var price = new TicketPrice
            {
                TierId = tier.Id, Name = "Regular", Price = 200_000m, Quota = 20, IsActive = true,
                SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
                SaleEnd = DateTimeOffset.UtcNow.AddDays(20),
                PurchaseChannel = PurchaseChannel.Online
            };
            db.Add(price);
            await db.SaveChangesAsync();
            priceId = price.Id;
        }

        var buyer = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var advertised = (await ReadPublishedPolicyAsync(buyer, showId)).RefundPercentage;
        advertised.Should().Be(40m);

        var hold = await (await buyer.PostAsJsonAsync("/api/v1/tickets/holds",
            new { PriceId = priceId, Quantity = 1 })).Content.ReadFromJsonAsync<Envelope<HoldData>>();
        var purchase = await (await buyer.PostAsJsonAsync("/api/v1/tickets/purchase",
            new { HoldId = hold!.Data.HoldId })).Content.ReadFromJsonAsync<Envelope<PurchaseData>>();

        await buyer.GetAsync(
            $"/api/v1/payments/vnpay/callback?vnp_TxnRef={purchase!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(purchase.Data.Amount * 100)}");

        var cancel = await buyer.PostAsync($"/api/v1/tickets/{purchase.Data.TicketIds[0]}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refund = await db.RefundRequests.SingleAsync(r => r.PaymentId == purchase.Data.PaymentId);

            refund.RefundPercentage.Should().Be(advertised);
            refund.AmountRequested.Should().Be(200_000m * 40m / 100m,
                "a policy the buyer was shown before paying has to be the arithmetic applied after");
        }
    }

    [Fact]
    public async Task ShowSoldAsNonRefundable_StillRefundsInFullWhenTheVenueCancels()
    {
        // This is the line the owner cannot cross. Every venue-at-fault path hardcodes 100% and none
        // of them reads RefundPercentage, which is precisely what makes it safe to let the owner set
        // the voluntary-change-of-mind policy however strictly they like.
        var client = Owner();

        var create = await client.PostAsJsonAsync("/api/v1/lounge-shows",
            CreateBody(cancellationAllowed: false));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var showId = (await create.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        var policy = await ReadPublishedPolicyAsync(client, showId);

        policy.CancellationAllowed.Should().BeFalse();
        policy.CancelBefore.Should().BeNull();
        policy.AlwaysFullRefundIfVenueCancels.Should().BeTrue();
        policy.Summary.Should().Contain("không cho phép tự hủy vé")
            .And.Contain("100%",
                "a buyer told \"no refunds\" must also be told about the guarantee that survives it");
    }

    [Theory]
    // "You may cancel, and you get nothing" is not a refund policy — it is "no refunds" written so
    // that the buyer has to work it out. It also produces a zero-value RefundRequest downstream.
    [InlineData(true, 0d, null, "lớn hơn 0")]
    [InlineData(true, 150d, null, "từ 0 đến 100")]
    [InlineData(true, 33.333, null, "2 chữ số thập phân")]
    // A deadline longer than the sale window is a cancellation right nobody can ever exercise.
    [InlineData(true, null, 24 * 30, "tối đa")]
    [InlineData(true, null, 0, "từ 1 giờ trở lên")]
    // Settings recorded against a show that ignores them would leave the owner believing they had
    // configured something.
    [InlineData(false, 50d, null, "không cho phép tự hủy vé")]
    [InlineData(false, null, 48, "không cho phép tự hủy vé")]
    public async Task IncoherentPolicy_IsRejectedWithAnExplanationTheOwnerCanAct(
        bool cancellationAllowed, double? percentage, int? deadlineHours, string expectedHint)
    {
        var res = await Owner().PostAsJsonAsync("/api/v1/lounge-shows", CreateBody(
            cancellationAllowed: cancellationAllowed,
            refundPercentage: percentage is double p ? (decimal)p : null,
            cancellationDeadlineHours: deadlineHours));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain(expectedHint);
    }

    [Fact]
    public async Task PolicyIsFrozenOnceTheShowIsLive_SoTermsShownAtPurchaseAreTermsEnforcedAtCancellation()
    {
        var client = Owner();

        var create = await client.PostAsJsonAsync("/api/v1/lounge-shows",
            CreateBody(cancellationAllowed: true, refundPercentage: 100m));
        var showId = (await create.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.LoungeShows.SingleAsync(s => s.Id == showId)).Status = LoungeShowStatus.Published;
            await db.SaveChangesAsync();
        }

        var edit = await client.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}", new
        {
            Name = "Đổi chính sách sau khi đã bán vé",
            Description = "Integration test show",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(30),
            ScheduledEnd = (DateTimeOffset?)null,
            TicketSaleClosesAt = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = (int?)null,
            OnlineQuota = (int?)null,
            CancellationAllowed = false
        });

        edit.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a policy that can be tightened after tickets are sold is not a policy the buyer agreed " +
            "to — the Draft-only guard is what makes disclosure mean anything");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.LoungeShows.SingleAsync(s => s.Id == showId)).CancellationAllowed
                .Should().BeTrue();
        }
    }

    [Fact]
    public async Task Rescheduling_DropsADeadlineTheNewDateHasAlreadyMadeImpossible()
    {
        // RescheduleLoungeShow re-opens cancellation because the venue moved the date. Before
        // MLACP-288 that was the whole story; now the show can also carry a deadline it inherited
        // from the old date, and a deadline further out than the new date is a reopened right that
        // is shut again on the same line.
        int showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"RescheduleDeadline-{Guid.NewGuid():N}",
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(30),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(30).AddHours(3),
                CancellationAllowed = true,
                CancellationDeadlineHours = TicketRefundPolicy.MaxCancellationDeadlineHours
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            showId = show.Id;
        }

        // 13 calendar days is comfortably past the 7-business-day republish floor, and closer than
        // the show's own 14-day cancellation deadline — so the deadline can no longer be met.
        var res = await Owner().PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/reschedule",
            new { NewScheduledStart = DateTimeOffset.UtcNow.AddDays(13) });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = await db.LoungeShows.SingleAsync(s => s.Id == showId);

            show.CancellationAllowed.Should().BeTrue();
            show.CancellationDeadlineHours.Should().BeNull(
                "keeping it would mean telling ticket holders their refund window reopened while " +
                "the endpoint rejected every one of them");
        }
    }

    [Fact]
    public void DeadlineReachableWhenTheDraftWasWritten_CanExpireBeforeItIsSubmitted()
    {
        // Why PublishLoungeShow re-checks rather than trusting the validators: nobody edits the show
        // in between, the clock simply moves.
        var show = new LoungeShow
        {
            CancellationAllowed = true,
            CancellationDeadlineHours = 72,
            ScheduledStart = DateTimeOffset.UtcNow.AddHours(48)
        };

        // Same show, same 72-hour deadline, judged 20 days ago: perfectly acceptable then.
        TicketRefundPolicy.Validate(
                cancellationAllowed: true, refundPercentage: null, cancellationDeadlineHours: 72,
                scheduledStart: show.ScheduledStart, now: DateTimeOffset.UtcNow.AddDays(-20))
            .Should().BeNull("72 hours before a show three weeks out was reachable when it was set");

        TicketRefundPolicy.IsDeadlineStillReachable(show, DateTimeOffset.UtcNow).Should().BeFalse(
            "the show is now 48 hours away, so a 72-hour deadline is behind us");

        show.CancellationDeadlineHours = null;
        TicketRefundPolicy.IsDeadlineStillReachable(show, DateTimeOffset.UtcNow).Should().BeTrue(
            "no deadline is always reachable");

        show.CancellationAllowed = false;
        show.CancellationDeadlineHours = 72;
        TicketRefundPolicy.IsDeadlineStillReachable(show, DateTimeOffset.UtcNow).Should().BeTrue(
            "a show that allows no cancellation at all advertises no window to miss");
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record ShowDetail(int Id, string Name, RefundPolicy? RefundPolicy);
    private sealed record RefundPolicy(
        bool CancellationAllowed, decimal RefundPercentage, DateTimeOffset? CancelBefore,
        int? DeadlineHoursBeforeStart, bool AlwaysFullRefundIfVenueCancels, string Summary);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record PurchaseData(
        int PaymentId, string OrderId, decimal Amount, string PaymentUrl, Guid[] TicketIds);
}
