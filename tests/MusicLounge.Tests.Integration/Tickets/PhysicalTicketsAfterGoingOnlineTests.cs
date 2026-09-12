using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// MLACP-383. <c>ChangeLoungeShowFormat</c> (Offline → Online) huỷ và hoàn 100% vé vào cửa đã bán (D13) — nhưng
/// không tắt tier vào cửa, và không điểm bán nào hỏi lại hình thức buổi diễn. Mỗi bài một phòng trà riêng.
/// </summary>
[Collection("Integration")]
public sealed class PhysicalTicketsAfterGoingOnlineTests
{
    private const string NoLongerOffered = "không còn bán vé vào cửa";

    private readonly ApiFactory _factory;

    public PhysicalTicketsAfterGoingOnlineTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record PurchaseData(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record IpnBody(string RspCode, string Message);
    private sealed record Venue(int OwnerId, int LoungeId, int ShowId, int OnlinePriceId, int CounterPriceId);

    private async Task<Venue> OfflineShowAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"t383-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = owner.Id,
            Name = $"Venue383-{Guid.NewGuid():N}"[..30],
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var show = new LoungeShow
        {
            LoungeId = lounge.Id,
            Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18],
            Description = "MLACP-383",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Vào cửa", AccessType = AccessType.Physical, TotalCapacity = 100
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        TicketPrice NewPrice(PurchaseChannel channel) => new()
        {
            TierId = tier.Id, Name = channel.ToString(), Price = 150_000m, Quota = 50, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(2),
            PurchaseChannel = channel
        };
        var online = NewPrice(PurchaseChannel.Online);
        var counter = NewPrice(PurchaseChannel.Offline);
        db.AddRange(online, counter);
        await db.SaveChangesAsync();

        return new Venue(owner.Id, lounge.Id, show.Id, online.Id, counter.Id);
    }

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

    private HttpClient Owner(Venue venue) => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);

    private async Task GoOnlineAsync(Venue venue)
        => (await Owner(venue).PutAsJsonAsync($"/api/v1/lounge-shows/{venue.ShowId}/format", new { NewFormat = "Online" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

    private Task<HttpResponseMessage> HoldAsync(int priceId)
        => Audience().PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = priceId, Quantity = 1 });

    private async Task<int> HoldIdAsync(int priceId)
    {
        var res = await HoldAsync(priceId);
        res.StatusCode.Should().Be(HttpStatusCode.Created, "test premise: an offline show sells entry tickets");
        return (await res.Content.ReadFromJsonAsync<Envelope<HoldData>>())!.Data.HoldId;
    }

    [Fact]
    public async Task AfterGoingOnline_NoOneCanHoldAnEntryTicket()
    {
        var venue = await OfflineShowAsync();
        await GoOnlineAsync(venue);

        var res = await HoldAsync(venue.OnlinePriceId);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "the venue just refunded every entry ticket because there is no longer a room to enter");
        (await res.Content.ReadAsStringAsync()).Should().Contain(NoLongerOffered);
    }

    [Fact]
    public async Task AHoldTakenBeforeGoingOnline_CannotBePaidAfterwards()
    {
        var venue = await OfflineShowAsync();
        var holdId = await HoldIdAsync(venue.OnlinePriceId);
        await GoOnlineAsync(venue);

        var res = await Audience().PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = holdId });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "the hold predates the change; the money would not");
        (await res.Content.ReadAsStringAsync()).Should().Contain(NoLongerOffered);

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Payments
                .AnyAsync(p => p.ReferenceType == "TicketHold" && p.ReferenceId == holdId.ToString()))
            .Should().BeFalse("no payment may be opened");
    }

    [Fact]
    public async Task TheBoxOfficeCannotSellEntryAfterGoingOnline()
    {
        var venue = await OfflineShowAsync();
        await GoOnlineAsync(venue);

        var res = await _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner")
            .PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = venue.CounterPriceId, Quantity = 1 });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "cash at the door for a seat that no longer exists is still money taken for nothing");
        (await res.Content.ReadAsStringAsync()).Should().Contain(NoLongerOffered);
    }

    [Fact]
    public async Task APaymentInFlightWhenTheShowWentOnline_IssuesNoEntryTicket_AndRefundsInFull()
    {
        var venue = await OfflineShowAsync();
        var holdId = await HoldIdAsync(venue.OnlinePriceId);
        var purchaseRes = await Audience().PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = holdId });
        purchaseRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var purchase = (await purchaseRes.Content.ReadFromJsonAsync<Envelope<PurchaseData>>())!.Data;

        await GoOnlineAsync(venue);

        var transactionNo = $"T{Guid.NewGuid():N}"[..16];
        var ipnRes = await _factory.CreateClient().GetAsync(
            $"/api/v1/payments/vnpay/ipn?vnp_TxnRef={purchase.OrderId}&vnp_ResponseCode=00" +
            $"&vnp_Amount={(long)(purchase.Amount * 100)}&vnp_TransactionNo={transactionNo}");
        (await ipnRes.Content.ReadFromJsonAsync<IpnBody>())!.RspCode.Should().Be("02");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Tickets.AsNoTracking().Where(t => t.PaymentId == purchase.PaymentId).ToListAsync())
            .Should().NotBeEmpty().And.OnlyContain(t => t.Status == TicketStatus.Cancelled,
                "an entry ticket for an online-only show must not be issued");
        (await db.Payments.AsNoTracking().SingleAsync(p => p.Id == purchase.PaymentId)).Status
            .Should().Be(PaymentStatus.Failed);
        var refund = (await db.RefundRequests.AsNoTracking().Where(r => r.PaymentId == purchase.PaymentId).ToListAsync())
            .Should().ContainSingle().Subject;
        refund.RefundPercentage.Should().Be(100m, "D13: going online refunds entry tickets in full");
        refund.AmountRequested.Should().Be(purchase.Amount);
        refund.RequestedBy.Should().Be(SeedHelper.AudienceId);
        (await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == SeedHelper.AudienceId && n.Type == NotificationType.EventFormatChanged
                            && n.ReferenceId == venue.ShowId.ToString())
                .ToListAsync())
            .Should().Contain(n => n.Body.Contains(transactionNo) && n.Body.Contains("hoàn 100%"));
    }
}
