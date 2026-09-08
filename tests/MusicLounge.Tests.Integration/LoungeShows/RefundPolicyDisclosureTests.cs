using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// Đợt-7 audit. LoungeShow carries the D13 cancellation-policy columns and CancelTicket enforces
/// them, but no DTO exposed any of them — so an audience member decided whether to buy without
/// being able to see whether the ticket was refundable, on what terms, or by when. NĐ 85/2021
/// requires the platform to publish its buyer-protection policy, and every comparable ticketing
/// platform treats publishing it as a precondition of selling.
///
/// The assertion that matters most here is not that the fields exist, but that what the show page
/// PROMISES equals what the cancel endpoint DOES. A platform advertising one refund policy and
/// applying another is worse off than one advertising nothing, so both sides now read the same
/// TicketRefundPolicy resolver and this test pins them together.
/// </summary>
[Collection("Integration")]
public sealed class RefundPolicyDisclosureTests
{
    private readonly ApiFactory _factory;

    public RefundPolicyDisclosureTests(ApiFactory factory) => _factory = factory;

    private async Task<(int ShowId, int PriceId)> SeedSellableShowAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"PolicyShow-{Guid.NewGuid():N}",
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
            AccessType = AccessType.Physical, TotalCapacity = 50
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Regular", Price = 150_000m, Quota = 50, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(4),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();

        return (show.Id, price.Id);
    }

    [Fact]
    public async Task ShowDetail_ExposesRefundPolicyBuyerCanReadBeforePaying()
    {
        var (showId, _) = await SeedSellableShowAsync();
        var client = _factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/lounge-shows/{showId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<Envelope<ShowDetail>>();
        var policy = body!.Data.RefundPolicy;

        policy.Should().NotBeNull("the buyer must be able to see the refund terms before paying");
        policy!.CancellationAllowed.Should().BeTrue();
        policy.RefundPercentage.Should().Be(100m);
        policy.AlwaysFullRefundIfVenueCancels.Should().BeTrue(
            "every venue-at-fault path hardcodes a full refund, and that is the protection worth " +
            "telling the buyer about");
        policy.Summary.Should().NotBeNullOrWhiteSpace(
            "the wording is built server-side so every client states identical terms");
        policy.Summary.Should().Contain("100%");
    }

    [Fact]
    public async Task AdvertisedRefundPercentage_EqualsWhatCancellingActuallyGrants()
    {
        var (showId, priceId) = await SeedSellableShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var detailRes = await client.GetAsync($"/api/v1/lounge-shows/{showId}");
        var advertised = (await detailRes.Content.ReadFromJsonAsync<Envelope<ShowDetail>>())!
            .Data.RefundPolicy!.RefundPercentage;

        // Buy a ticket the honest way, then cancel it and compare what was actually granted.
        var holdRes = await client.PostAsJsonAsync("/api/v1/tickets/holds",
            new { PriceId = priceId, Quantity = 1 });
        var hold = await holdRes.Content.ReadFromJsonAsync<Envelope<HoldData>>();

        var purchaseRes = await client.PostAsJsonAsync("/api/v1/tickets/purchase",
            new { HoldId = hold!.Data.HoldId });
        var purchase = await purchaseRes.Content.ReadFromJsonAsync<Envelope<PurchaseData>>();

        await client.GetAsync(
            $"/api/v1/payments/vnpay/callback?vnp_TxnRef={purchase!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(purchase.Data.Amount * 100)}");

        var ticketId = purchase.Data.TicketIds[0];
        var cancelRes = await client.PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);
        cancelRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var refund = await db.RefundRequests
            .SingleAsync(r => r.PaymentId == purchase.Data.PaymentId);

        refund.RefundPercentage.Should().Be(advertised,
            "the percentage shown on the show page before purchase must be exactly the percentage " +
            "the cancel endpoint grants — otherwise the platform advertises terms it does not honour");
        refund.AmountRequested.Should().Be(150_000m * advertised / 100m);
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
