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
/// MLACP-389. Tiền VNPay về khi buổi diễn đã kết thúc (mua lúc đang diễn, IPN về muộn — VNPay gọi lại tới ~50 phút).
/// Vé của giao dịch đó còn Pending nên chưa từng soát được (soát vé đòi show Ongoing và vé Confirmed) — cấp vé lúc này là
/// bán vé cho một buổi diễn đã xong. Trừ vé livestream còn xem lại được bản ghi. Mỗi bài một phòng trà riêng.
/// </summary>
[Collection("Integration")]
public sealed class PaymentAfterShowEndedTests
{
    private readonly ApiFactory _factory;

    public PaymentAfterShowEndedTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record PurchaseData(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record IpnBody(string RspCode, string Message);

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private const string Recording = "https://stream.mux.com/replay389.m3u8";

    /// <param name="access">Loại vé đem bán.</param>
    /// <param name="livestream">null = buổi diễn trực tiếp, không livestream. Có giá trị = buổi diễn có một livestream đã
    /// phát xong, với bản ghi (null = Mux chưa báo asset.ready) và hạn xem lại; hình thức là Online khi bán vé livestream,
    /// Hybrid khi bán vé vào cửa.</param>
    private async Task<(int ShowId, int PriceId)> ShowAsync(
        AccessType access, (string? RecordingUrl, DateTimeOffset? ReplayUntil)? livestream)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var owner = new User { Email = $"t389-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeEntity
        {
            OwnerId = owner.Id, Name = $"Venue389-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18], Description = "MLACP-389",
            Format = livestream is null ? LoungeShowFormat.Offline
                : access == AccessType.Livestream ? LoungeShowFormat.Online : LoungeShowFormat.Hybrid,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(2), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(2).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        if (livestream is { } ls)
            db.Add(new Livestream
            {
                LoungeShowId = show.Id, Status = LivestreamStatus.Ended, IsFree = false,
                StartedAt = DateTimeOffset.UtcNow.AddHours(-3), EndedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                RecordingUrl = ls.RecordingUrl, ReplayAvailableUntil = ls.ReplayUntil
            });
        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = access == AccessType.Physical ? "Vào cửa" : "Xem online",
            AccessType = access, TotalCapacity = 100
        };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = 90_000m, Quota = 100, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(1),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();
        return (show.Id, price.Id);
    }

    private async Task<PurchaseData> StartPaymentAsync(int priceId)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var hold = await client.PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = priceId, Quantity = 1 });
        hold.StatusCode.Should().Be(HttpStatusCode.Created, await hold.Content.ReadAsStringAsync());
        var holdId = (await hold.Content.ReadFromJsonAsync<Envelope<HoldData>>())!.Data.HoldId;
        var purchase = await client.PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = holdId });
        purchase.StatusCode.Should().Be(HttpStatusCode.Created, await purchase.Content.ReadAsStringAsync());
        return (await purchase.Content.ReadFromJsonAsync<Envelope<PurchaseData>>())!.Data;
    }

    private async Task SetShowStatusAsync(int showId, LoungeShowStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        (await db.LoungeShows.SingleAsync(s => s.Id == showId)).Status = status;
        await db.SaveChangesAsync();
    }

    /// <summary>VNPay gọi IPN báo khách đã trả đủ.</summary>
    private async Task<IpnBody> PaidIpnAsync(PurchaseData purchase)
    {
        var transactionNo = $"T{Guid.NewGuid():N}"[..16];
        var res = await _factory.CreateClient().GetAsync(
            $"/api/v1/payments/vnpay/ipn?vnp_TxnRef={purchase.OrderId}&vnp_ResponseCode=00" +
            $"&vnp_Amount={(long)(purchase.Amount * 100)}&vnp_TransactionNo={transactionNo}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<IpnBody>())!;
    }

    private async Task<(List<Ticket> Tickets, List<RefundRequest> Refunds)> StateAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        return (await db.Tickets.AsNoTracking().Where(t => t.PaymentId == paymentId).ToListAsync(),
                await db.RefundRequests.AsNoTracking().Where(r => r.PaymentId == paymentId).ToListAsync());
    }

    [Fact]
    public async Task AnEntryTicketPaidAfterTheShowEnded_IsNotIssued_AndRefundedInFull()
    {
        var (showId, priceId) = await ShowAsync(AccessType.Physical, livestream: null);
        var purchase = await StartPaymentAsync(priceId);
        await SetShowStatusAsync(showId, LoungeShowStatus.Ended);

        (await PaidIpnAsync(purchase)).RspCode.Should().Be("02");

        var (tickets, refunds) = await StateAsync(purchase.PaymentId);
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Cancelled,
            "a ticket that could never be scanned delivers nothing once the show is over");
        refunds.Should().ContainSingle().Which.RefundPercentage.Should().Be(100m);

        using var scope = _factory.Services.CreateScope();
        (await Db(scope).Notifications.AsNoTracking().AnyAsync(n =>
                n.UserId == SeedHelper.AudienceId && n.Type == NotificationType.RefundUpdate
                && n.ReferenceId == showId.ToString() && n.Body.Contains("đã kết thúc")))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ALivestreamTicketPaidAfterTheShowEnded_IsStillIssued_WhileTheReplayIsAvailable()
    {
        var (showId, priceId) = await ShowAsync(AccessType.Livestream, (Recording, DateTimeOffset.UtcNow.AddDays(7)));
        var purchase = await StartPaymentAsync(priceId);
        await SetShowStatusAsync(showId, LoungeShowStatus.Ended);

        (await PaidIpnAsync(purchase)).RspCode.Should().Be("00");

        var (tickets, refunds) = await StateAsync(purchase.PaymentId);
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Confirmed,
            "the buyer can still watch the recording they paid for");
        refunds.Should().BeEmpty();
    }

    [Fact]
    public async Task ALivestreamTicketPaidAfterTheShowEnded_IsRefunded_WhileThereIsNoRecordingToWatch()
    {
        // Livestream đã phát nhưng Mux chưa báo bản ghi: không hứa trước một bản ghi có thể không bao giờ có.
        var (showId, priceId) = await ShowAsync(AccessType.Livestream, livestream: (null, null));
        var purchase = await StartPaymentAsync(priceId);
        await SetShowStatusAsync(showId, LoungeShowStatus.Ended);

        (await PaidIpnAsync(purchase)).RspCode.Should().Be("02");

        var (tickets, refunds) = await StateAsync(purchase.PaymentId);
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Cancelled, "there is nothing recorded to watch");
        refunds.Should().ContainSingle().Which.RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task ALivestreamTicketPaidAfterTheShowEnded_IsRefunded_OnceTheReplayHasExpired()
    {
        var (showId, priceId) = await ShowAsync(AccessType.Livestream, (Recording, DateTimeOffset.UtcNow.AddDays(-1)));
        var purchase = await StartPaymentAsync(priceId);
        await SetShowStatusAsync(showId, LoungeShowStatus.Ended);

        (await PaidIpnAsync(purchase)).RspCode.Should().Be("02");

        var (tickets, refunds) = await StateAsync(purchase.PaymentId);
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Cancelled,
            "the viewer would be refused the recording the ticket was sold for");
        refunds.Should().ContainSingle().Which.RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task AnEntryTicketOfAHybridShow_IsRefunded_EvenThoughARecordingExists()
    {
        // Vé vào cửa bán chỗ ngồi tối nay, không bán bản ghi — bản ghi của phần livestream không thay được thứ đã mua.
        var (showId, priceId) = await ShowAsync(AccessType.Physical, (Recording, DateTimeOffset.UtcNow.AddDays(7)));
        var purchase = await StartPaymentAsync(priceId);
        await SetShowStatusAsync(showId, LoungeShowStatus.Ended);

        (await PaidIpnAsync(purchase)).RspCode.Should().Be("02");

        var (tickets, refunds) = await StateAsync(purchase.PaymentId);
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Cancelled);
        refunds.Should().ContainSingle().Which.RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task AnEntryTicketPaidWhileTheShowIsOn_IsIssuedAsBefore()
    {
        var (showId, priceId) = await ShowAsync(AccessType.Physical, livestream: null);
        var purchase = await StartPaymentAsync(priceId);
        await SetShowStatusAsync(showId, LoungeShowStatus.Ongoing);

        (await PaidIpnAsync(purchase)).RspCode.Should().Be("00");

        var (tickets, refunds) = await StateAsync(purchase.PaymentId);
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Confirmed, "the buyer can still get in tonight");
        refunds.Should().BeEmpty();
    }
}
