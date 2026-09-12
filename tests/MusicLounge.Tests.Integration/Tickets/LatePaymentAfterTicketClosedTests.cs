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
/// MLACP-385. Khách tự huỷ vé đang chờ thanh toán (hoặc <c>CancelAbandonedPaymentsJob</c> đóng thanh toán quá hạn),
/// rồi VNPay vẫn báo đã thu tiền. Trước task này IPN chỉ báo Admin (MLACP-334): không tạo yêu cầu hoàn, không báo
/// khách — tiền treo tới khi có người xử lý tay.
///
/// <para>Nay: (1) chặn từ gốc — không huỷ vé khi link VNPay của nó còn hiệu lực (Stripe: huỷ trước khi hoàn tất;
/// F&amp;B MLACP-349); (2) lưới an toàn — tiền về cho đơn đã đóng mà chưa cấp vé nào thì tự tạo yêu cầu hoàn 100%,
/// vẫn không tự cấp lại vé. Mỗi bài một phòng trà riêng.</para>
/// </summary>
[Collection("Integration")]
public sealed class LatePaymentAfterTicketClosedTests
{
    private readonly ApiFactory _factory;

    public LatePaymentAfterTicketClosedTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record PurchaseData(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record IpnBody(string RspCode, string Message);

    private async Task<int> PriceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"t385-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = owner.Id, Name = $"Venue385-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18], Description = "MLACP-385",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier { LoungeShowId = show.Id, Name = "Vào cửa", AccessType = AccessType.Physical, TotalCapacity = 100 };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = 120_000m, Quota = 100, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(2),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();
        return price.Id;
    }

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

    /// <summary>Giữ chỗ + bấm thanh toán: Payment Pending, vé Pending, link VNPay vừa mở.</summary>
    private async Task<PurchaseData> StartPaymentAsync(int quantity = 1)
    {
        var priceId = await PriceAsync();
        var holdRes = await Audience().PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = priceId, Quantity = quantity });
        holdRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var hold = (await holdRes.Content.ReadFromJsonAsync<Envelope<HoldData>>())!.Data;
        var purchaseRes = await Audience().PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = hold.HoldId });
        purchaseRes.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await purchaseRes.Content.ReadFromJsonAsync<Envelope<PurchaseData>>())!.Data;
    }

    /// <summary>Đẩy thời điểm tạo link về quá khứ — link VNPay (15 phút) coi như đã hết hạn.</summary>
    private async Task AgePaymentAsync(int paymentId, TimeSpan by)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db.Payments.SingleAsync(p => p.Id == paymentId);
        payment.CreatedAt = payment.CreatedAt - by;
        await db.SaveChangesAsync();
    }

    private async Task<List<Ticket>> TicketsAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Tickets.AsNoTracking()
            .Where(t => t.PaymentId == paymentId).OrderBy(t => t.Id).ToListAsync();
    }

    private async Task<IpnBody> PaidIpnAsync(PurchaseData purchase, string transactionNo, decimal? amount = null)
    {
        var res = await _factory.CreateClient().GetAsync(
            $"/api/v1/payments/vnpay/ipn?vnp_TxnRef={purchase.OrderId}&vnp_ResponseCode=00" +
            $"&vnp_Amount={(long)((amount ?? purchase.Amount) * 100)}&vnp_TransactionNo={transactionNo}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<IpnBody>())!;
    }

    private static string NewTransactionNo() => $"T{Guid.NewGuid():N}"[..16];

    // ── Chặn từ gốc ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CancellingAPendingTicket_WhileItsVnPayLinkIsLive_IsRefused()
    {
        var purchase = await StartPaymentAsync();
        var ticket = (await TicketsAsync(purchase.PaymentId)).Single();

        var res = await Audience().PostAsync($"/api/v1/tickets/{ticket.Id}/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "VNPay cannot void an open link — cancelling now does not stop the buyer being charged");
        (await res.Content.ReadAsStringAsync()).Should().Contain("còn hiệu lực");
        (await TicketsAsync(purchase.PaymentId)).Single().Status.Should().Be(TicketStatus.Pending);
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Payments.AsNoTracking()
                .SingleAsync(p => p.Id == purchase.PaymentId)).Status
            .Should().Be(PaymentStatus.Pending, "the refused cancel must change nothing");
    }

    [Fact]
    public async Task CancellingAfterTheLinkExpired_StillWorks()
    {
        var purchase = await StartPaymentAsync();
        await AgePaymentAsync(purchase.PaymentId, TimeSpan.FromMinutes(20));
        var ticket = (await TicketsAsync(purchase.PaymentId)).Single();

        var res = await Audience().PostAsync($"/api/v1/tickets/{ticket.Id}/cancel", null);

        res.IsSuccessStatusCode.Should().BeTrue("the block only lasts as long as the link can still be paid");
        (await TicketsAsync(purchase.PaymentId)).Single().Status.Should().Be(TicketStatus.Cancelled);
    }

    // ── Lưới an toàn ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MoneyArrivingAfterTheBuyerCancelledOneOfTwoTickets_RefundsTheWholePayment_AndIssuesNothing()
    {
        var purchase = await StartPaymentAsync(quantity: 2);
        await AgePaymentAsync(purchase.PaymentId, TimeSpan.FromMinutes(20));
        var first = (await TicketsAsync(purchase.PaymentId)).First();
        (await Audience().PostAsync($"/api/v1/tickets/{first.Id}/cancel", null)).IsSuccessStatusCode.Should().BeTrue();
        var transactionNo = NewTransactionNo();

        (await PaidIpnAsync(purchase, transactionNo)).RspCode.Should().Be("02");
        (await PaidIpnAsync(purchase, transactionNo)).RspCode.Should().Be("02", "a replay changes nothing");

        (await TicketsAsync(purchase.PaymentId)).Should().HaveCount(2)
            .And.OnlyContain(t => t.Status == TicketStatus.Cancelled,
                "the order was closed — the seat may be gone, so nothing is re-issued (MLACP-334)");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == purchase.PaymentId);
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.TransactionId.Should().Be(transactionNo, "the VNPay refund needs the real transaction number");

        var refund = (await db.RefundRequests.AsNoTracking().Where(r => r.PaymentId == purchase.PaymentId).ToListAsync())
            .Should().ContainSingle("the replay must not queue a second refund").Subject;
        refund.RefundPercentage.Should().Be(100m);
        refund.AmountRequested.Should().Be(purchase.Amount, "the buyer received nothing for any of it");
        refund.RequestedBy.Should().Be(SeedHelper.AudienceId);

        (await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == SeedHelper.AudienceId && n.Type == NotificationType.RefundUpdate)
                .ToListAsync())
            .Should().Contain(n => n.Body.Contains(transactionNo) && n.Body.Contains("hoàn 100%"),
                "the buyer who was charged is told the money is coming back");
        (await db.Notifications.AsNoTracking()
                .AnyAsync(n => n.Type == NotificationType.PaymentConfirmedAfterExpiry
                               && n.ReferenceId == purchase.PaymentId.ToString()))
            .Should().BeTrue("Admins are still told, and can still re-issue by hand");
    }

    [Fact]
    public async Task MoneyArrivingAfterTheAbandonedPaymentJobClosedIt_IsRefundedToo()
    {
        var purchase = await StartPaymentAsync();
        await AgePaymentAsync(purchase.PaymentId, TimeSpan.FromHours(3));
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<CancelAbandonedPaymentsJob>()
                .ExecuteAsync(new JobCancellationToken(false));
        (await TicketsAsync(purchase.PaymentId)).Single().Status
            .Should().Be(TicketStatus.Cancelled, "test premise: the job closed the payment");

        (await PaidIpnAsync(purchase, NewTransactionNo())).RspCode.Should().Be("02");

        using var check = _factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefundRequests.AsNoTracking()
                .Where(r => r.PaymentId == purchase.PaymentId).ToListAsync())
            .Should().ContainSingle().Which.RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task AWrongAmount_IsNotAutoRefunded_OnlyReportedToAdmins()
    {
        var purchase = await StartPaymentAsync();
        await AgePaymentAsync(purchase.PaymentId, TimeSpan.FromMinutes(20));
        var ticket = (await TicketsAsync(purchase.PaymentId)).Single();
        (await Audience().PostAsync($"/api/v1/tickets/{ticket.Id}/cancel", null)).IsSuccessStatusCode.Should().BeTrue();

        await PaidIpnAsync(purchase, NewTransactionNo(), amount: purchase.Amount + 1_000m);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.RefundRequests.AsNoTracking().AnyAsync(r => r.PaymentId == purchase.PaymentId))
            .Should().BeFalse("an amount that does not match what was asked needs a person to reconcile first");
        (await db.Notifications.AsNoTracking()
                .AnyAsync(n => n.Type == NotificationType.PaymentConfirmedAfterExpiry
                               && n.ReferenceId == purchase.PaymentId.ToString()))
            .Should().BeTrue();
    }
}
