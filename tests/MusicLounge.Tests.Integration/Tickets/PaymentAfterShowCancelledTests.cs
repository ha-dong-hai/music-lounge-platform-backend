using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// MLACP-382. Khách bấm thanh toán (vé Pending), buổi diễn bị huỷ trong lúc khách còn trên trang VNPay —
/// <c>ShowCancellation</c> chỉ hoàn vé đã Confirmed nên bỏ qua vé này — rồi tiền về. Trước task này
/// <c>ProcessVnPayCallback</c> không nhìn buổi diễn: vé bị chuyển Confirmed cho một buổi diễn không còn tổ chức,
/// khách bị trừ tiền và không ai tạo yêu cầu hoàn.
///
/// <para>Nay: không cấp vé, tự tạo yêu cầu hoàn 100% đứng tên người trả — đúng mẫu F&amp;B đã chốt
/// (<c>ProcessFnbOrderPayment.RecordNotAppliedAsync</c>, MLACP-351). Mỗi bài một phòng trà riêng.</para>
/// </summary>
[Collection("Integration")]
public sealed class PaymentAfterShowCancelledTests
{
    private readonly ApiFactory _factory;

    public PaymentAfterShowCancelledTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record PurchaseData(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record IpnBody(string RspCode, string Message);
    private sealed record Venue(int OwnerId, int LoungeId, int ShowId, int PriceId);

    private async Task<Venue> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"t382-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = owner.Id,
            Name = $"Venue382-{Guid.NewGuid():N}"[..30],
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var show = new LoungeShow
        {
            LoungeId = lounge.Id,
            Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18],
            Description = "MLACP-382",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Standard", AccessType = AccessType.Physical, TotalCapacity = 100
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Regular", Price = 200_000m, Quota = 100, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(2),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();

        return new Venue(owner.Id, lounge.Id, show.Id, price.Id);
    }

    /// <summary>Giữ chỗ + bấm thanh toán: Payment Pending, vé Pending, link VNPay đã mở.</summary>
    private async Task<PurchaseData> StartPaymentAsync(int priceId)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var holdRes = await client.PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = priceId, Quantity = 2 });
        holdRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var hold = (await holdRes.Content.ReadFromJsonAsync<Envelope<HoldData>>())!.Data;

        var purchaseRes = await client.PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = hold.HoldId });
        purchaseRes.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await purchaseRes.Content.ReadFromJsonAsync<Envelope<PurchaseData>>())!.Data;
    }

    private async Task CancelShowAsOwnerAsync(Venue venue)
    {
        var res = await _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId)
            .PostAsync($"/api/v1/lounge-shows/{venue.ShowId}/cancel", null);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>VNPay gọi IPN báo khách đã trả đủ.</summary>
    private async Task<IpnBody> PaidIpnAsync(PurchaseData purchase, string transactionNo)
    {
        var res = await _factory.CreateClient().GetAsync(
            $"/api/v1/payments/vnpay/ipn?vnp_TxnRef={purchase.OrderId}&vnp_ResponseCode=00" +
            $"&vnp_Amount={(long)(purchase.Amount * 100)}&vnp_TransactionNo={transactionNo}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<IpnBody>())!;
    }

    private static string NewTransactionNo() => $"T{Guid.NewGuid():N}"[..16];

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    [Fact]
    public async Task PaymentArrivingAfterOwnerCancelledShow_IssuesNoTicket_AndQueuesAFullRefund()
    {
        var venue = await VenueAsync();
        var purchase = await StartPaymentAsync(venue.PriceId);
        await CancelShowAsOwnerAsync(venue);
        var transactionNo = NewTransactionNo();

        var ipn = await PaidIpnAsync(purchase, transactionNo);

        ipn.RspCode.Should().Be("02", "the incident is recorded — VNPay retrying would only hit the same dead end");

        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var payment = await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == purchase.OrderId);
        var tickets = await db.Tickets.AsNoTracking().Where(t => t.PaymentId == payment.Id).ToListAsync();

        tickets.Should().NotBeEmpty();
        tickets.Should().OnlyContain(t => t.Status == TicketStatus.Cancelled,
            "a ticket to a show nobody will put on must not be issued");
        payment.Status.Should().Be(PaymentStatus.Failed, "Failed here means 'not applied to any ticket'");
        payment.TransactionId.Should().Be(transactionNo, "the VNPay refund call needs the real transaction number");

        var refund = (await db.RefundRequests.AsNoTracking().Where(r => r.PaymentId == payment.Id).ToListAsync())
            .Should().ContainSingle().Subject;
        refund.RefundPercentage.Should().Be(100m);
        refund.AmountRequested.Should().Be(purchase.Amount);
        refund.RequestedBy.Should().Be(SeedHelper.AudienceId);
        refund.Status.Should().Be(RefundRequestStatus.Pending);

        (await db.LedgerEntries.AsNoTracking().AnyAsync(e => e.PaymentId == payment.Id))
            .Should().BeFalse("nothing was sold, so no purchase journal may be written");
        (await db.Settlements.AsNoTracking().AnyAsync(s => s.PaymentId == payment.Id))
            .Should().BeFalse("the venue must not be scheduled a payout for a cancelled show");

        (await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == SeedHelper.AudienceId && n.Type == NotificationType.EventCancelled
                            && n.ReferenceId == venue.ShowId.ToString())
                .ToListAsync())
            .Should().Contain(n => n.Body.Contains(transactionNo) && n.Body.Contains("hoàn 100%"),
                "the buyer who was just charged must be told the ticket was not issued and the money is coming back");
    }

    [Fact]
    public async Task TheQueuedRefund_CanActuallyBeApproved_WithoutReversingAJournalThatWasNeverWritten()
    {
        var venue = await VenueAsync();
        var purchase = await StartPaymentAsync(venue.PriceId);
        await CancelShowAsOwnerAsync(venue);
        await PaidIpnAsync(purchase, NewTransactionNo());

        int refundId;
        using (var scope = _factory.Services.CreateScope())
            refundId = (await Db(scope).RefundRequests.AsNoTracking()
                .SingleAsync(r => r.PaymentId == purchase.PaymentId)).Id;

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = (decimal?)null });

        res.IsSuccessStatusCode.Should().BeTrue(
            "a refund request nobody can process is only a promise — " + await res.Content.ReadAsStringAsync());

        using var check = _factory.Services.CreateScope();
        var db = Db(check);
        (await db.RefundRequests.AsNoTracking().SingleAsync(r => r.Id == refundId)).Status
            .Should().Be(RefundRequestStatus.Approved);
        (await db.Payments.AsNoTracking().SingleAsync(p => p.Id == purchase.PaymentId)).Status
            .Should().Be(PaymentStatus.Refunded);
        (await db.LedgerEntries.AsNoTracking().AnyAsync(e => e.PaymentId == purchase.PaymentId))
            .Should().BeFalse("reversing a purchase that was never booked would debit the platform for money it never held");
    }

    [Fact]
    public async Task ARepeatedCallback_QueuesNoSecondRefund_AndDoesNotAlertAdminsAgain()
    {
        var venue = await VenueAsync();
        var purchase = await StartPaymentAsync(venue.PriceId);
        await CancelShowAsOwnerAsync(venue);
        var transactionNo = NewTransactionNo();

        // Trình duyệt khách quay về và VNPay gọi IPN cho cùng một giao dịch — chuyện bình thường, không phải lỗi.
        (await PaidIpnAsync(purchase, transactionNo)).RspCode.Should().Be("02");
        (await PaidIpnAsync(purchase, transactionNo)).RspCode.Should().Be("02");

        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        (await db.RefundRequests.AsNoTracking().CountAsync(r => r.PaymentId == purchase.PaymentId)).Should().Be(1);

        var alerts = await db.Notifications.AsNoTracking()
            .Where(n => n.Type == NotificationType.PaymentConfirmedAfterExpiry
                        && n.ReferenceType == "payment" && n.ReferenceId == purchase.PaymentId.ToString())
            .ToListAsync();
        alerts.Should().NotBeEmpty("Admins are told once that money arrived for a cancelled show");
        alerts.GroupBy(n => n.UserId).Should().OnlyContain(g => g.Count() == 1,
            "a replay of an already-recorded incident must not page every Admin again");
    }

    [Fact]
    public async Task PaymentArrivingAfterABanCancelledTheShow_IsHandledTheSameWay()
    {
        var venue = await VenueAsync();
        var purchase = await StartPaymentAsync(venue.PriceId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            db.VenuePenalties.Add(new VenuePenalty
            {
                LoungeId = venue.LoungeId, PenaltyType = PenaltyType.Ban, Reason = "Vi phạm nghiêm trọng",
                IssuedBy = SeedHelper.AdminId, IssuedAt = DateTimeOffset.UtcNow.AddDays(-8),
                EffectiveAt = DateTimeOffset.UtcNow.AddMinutes(-1), Status = PenaltyStatus.Active
            });
            await db.SaveChangesAsync();
        }
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplyDuePenaltiesJob>()
                .ExecuteAsync(new JobCancellationToken(false));

        await PaidIpnAsync(purchase, NewTransactionNo());

        using var check = _factory.Services.CreateScope();
        var checkDb = Db(check);
        (await checkDb.LoungeShows.AsNoTracking().SingleAsync(s => s.Id == venue.ShowId)).Status
            .Should().Be(LoungeShowStatus.Cancelled, "test premise: the ban cancelled the show");
        (await checkDb.Tickets.AsNoTracking().Where(t => t.PaymentId == purchase.PaymentId).ToListAsync())
            .Should().OnlyContain(t => t.Status == TicketStatus.Cancelled);
        (await checkDb.RefundRequests.AsNoTracking().Where(r => r.PaymentId == purchase.PaymentId).ToListAsync())
            .Should().ContainSingle().Which.RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task ShowStillOn_PaymentConfirmsTheTickets_AsBefore()
    {
        var venue = await VenueAsync();
        var purchase = await StartPaymentAsync(venue.PriceId);

        var ipn = await PaidIpnAsync(purchase, NewTransactionNo());

        ipn.RspCode.Should().Be("00");
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        (await db.Tickets.AsNoTracking().Where(t => t.PaymentId == purchase.PaymentId).ToListAsync())
            .Should().OnlyContain(t => t.Status == TicketStatus.Confirmed, "the new check must not block a normal sale");
        (await db.RefundRequests.AsNoTracking().AnyAsync(r => r.PaymentId == purchase.PaymentId)).Should().BeFalse();
    }
}
