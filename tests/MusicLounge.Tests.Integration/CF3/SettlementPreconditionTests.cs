using System.Net;
using FluentAssertions;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// Đợt-3 audit. ScheduleSettlementHandler fails closed when the venue has no default BankAccount —
/// correct in itself, but it runs as a MediatR notification handler inside ProcessVnPayCallback's
/// transaction, and nothing anywhere requires a venue to register a bank account before it can
/// publish a show and sell tickets. PublishLoungeShow checks seven preconditions (venue standing,
/// Draft status, ≥1 tier, ≥1 performer, NĐ 144/2020 legal approval reference, 7-business-day lead
/// time, livestream record) and a payout account is not among them.
///
/// The result is the worst possible failure mode: VNPay has already taken the buyer's money, the
/// callback then rolls the whole confirmation back, the payment stays Pending, VNPay's retries keep
/// hitting the same exception, and 30 minutes later CancelAbandonedPaymentsJob marks the payment
/// Failed and cancels the tickets. Money taken, no ticket, no refund, nothing but a log line.
///
/// Every existing test misses this because SeedHelper gives every seeded lounge a default bank
/// account, so the precondition is never actually exercised.
/// </summary>
[Collection("Integration")]
public sealed class SettlementPreconditionTests
{
    private readonly ApiFactory _factory;

    public SettlementPreconditionTests(ApiFactory factory) => _factory = factory;

    /// <summary>Builds a Published show on a brand-new lounge that has NO bank account at all.</summary>
    private async Task<(int PriceId, int ShowId)> SeedShowOnVenueWithoutBankAccountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = freshOwner.Id,
            Name = $"NoBankVenue-{Guid.NewGuid():N}",
            Description = "Venue that never registered a payout account",
            Status = LoungeStatus.Approved
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        (await db.Set<BankAccount>().AnyAsync(
            a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == lounge.Id))
            .Should().BeFalse("test premise: this venue must have no payout account");

        var show = new LoungeShow
        {
            LoungeId = lounge.Id,
            Name = $"NoBankShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id,
            Name = "Standard",
            AccessType = AccessType.Physical,
            TotalCapacity = 100
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id,
            Name = "Regular",
            Price = 200_000m,
            Quota = 100,
            IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(2),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();

        return (price.Id, show.Id);
    }

    [Fact]
    public async Task VnPayCallback_WhenVenueHasNoBankAccount_DoesNotLeaveBuyerChargedWithoutTicket()
    {
        var (priceId, _) = await SeedShowOnVenueWithoutBankAccountAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var holdRes = await client.PostAsJsonAsync("/api/v1/tickets/holds",
            new { PriceId = priceId, Quantity = 1 });
        holdRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var hold = await holdRes.Content.ReadFromJsonAsync<Envelope<HoldData>>();

        var purchaseRes = await client.PostAsJsonAsync("/api/v1/tickets/purchase",
            new { HoldId = hold!.Data.HoldId });
        purchaseRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var purchase = await purchaseRes.Content.ReadFromJsonAsync<Envelope<PurchaseData>>();

        // VNPay says the buyer paid, in full. From here the money has left their account.
        await client.GetAsync(
            $"/api/v1/payments/vnpay/callback?vnp_TxnRef={purchase!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(purchase.Data.Amount * 100)}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db.Payments.SingleAsync(p => p.OrderId == purchase.Data.OrderId);
        var tickets = await db.Tickets.Where(t => t.PaymentId == payment.Id).ToListAsync();

        payment.Status.Should().NotBe(PaymentStatus.Pending,
            "a successful VNPay callback must reach a decided state — left Pending, the buyer has " +
            "paid and CancelAbandonedPaymentsJob will silently void their tickets 30 minutes later");
        tickets.Should().NotBeEmpty();
        tickets.Should().AllSatisfy(t => t.Status.Should().Be(TicketStatus.Confirmed,
            "the buyer paid in full, so they must end up holding the tickets they paid for"));
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record PurchaseData(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);
}
