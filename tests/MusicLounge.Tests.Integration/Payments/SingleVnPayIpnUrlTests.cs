using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-394. VNPay gắn URL IPN theo terminal, không theo giao dịch, mà cả bốn luồng thanh toán online dùng chung một
/// <c>TmnCode</c>. Nên <c>/api/v1/payments/vnpay/ipn</c> là URL IPN duy nhất được đăng ký: IPN của vé, F&amp;B, gói dịch
/// vụ và donate đều vào đây và phải tới đúng luồng của nó. Và handler của vé không được "xác nhận" một khoản không phải
/// tiền vé — trước task này nó tra thanh toán chỉ theo mã giao dịch, nên IPN của F&amp;B lạc vào đó bị đánh dấu đã trả
/// mà không có gì của F&amp;B được làm.
/// </summary>
[Collection("Integration")]
public sealed class SingleVnPayIpnUrlTests
{
    private const string IpnUrl = "/api/v1/payments/vnpay/ipn";

    private readonly ApiFactory _factory;

    public SingleVnPayIpnUrlTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record PurchaseData(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record FnbPaymentInit(int OrderId, string PaymentGatewayOrderId, decimal Amount, string PaymentUrl);
    private sealed record DonationInit(int DonationId, string OrderId);
    private sealed record IpnBody(string RspCode, string Message);

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private static string PaidQuery(string txnRef, decimal amount)
        => $"vnp_TxnRef={Uri.EscapeDataString(txnRef)}&vnp_ResponseCode=00" +
           $"&vnp_Amount={(long)(amount * 100)}&vnp_TransactionNo={$"U{Guid.NewGuid():N}"[..16]}";

    private async Task<IpnBody> PaidIpnAsync(string txnRef, decimal amount)
    {
        var res = await _factory.CreateClient().GetAsync($"{IpnUrl}?{PaidQuery(txnRef, amount)}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<IpnBody>())!;
    }

    private async Task<(int OwnerId, int LoungeId)> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var owner = new User { Email = $"ipn394-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue394-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return (owner.Id, lounge.Id);
    }

    /// <summary>Đơn F&amp;B ở phòng trà mẫu, khách bấm trả online: có một Payment Pending mã <c>FNB-…</c>.</summary>
    private async Task<FnbPaymentInit> PendingFnbPaymentAsync()
    {
        int menuItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var menu = new FnbMenu { LoungeId = SeedHelper.LoungeId, Name = "Menu MLACP-394", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Add(menu);
            await db.SaveChangesAsync();
            var item = new FnbMenuItem { MenuId = menu.Id, Category = "Drink", Name = "Trà đào", Price = 60_000m, IsAvailable = true };
            db.Add(item);
            await db.SaveChangesAsync();
            menuItemId = item.Id;
        }

        var audience = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var orderRes = await audience.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId, ShowId = (int?)null, ZoneId = (int?)null, TableNote = "Bàn 394",
            PaymentMethod = "Cash", Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity = 2, Note = (string?)null } }
        });
        orderRes.StatusCode.Should().Be(HttpStatusCode.Created, await orderRes.Content.ReadAsStringAsync());
        var orderId = (await orderRes.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;
        var payRes = await audience.PostAsync($"/api/v1/fnb-orders/{orderId}/pay", null);
        payRes.StatusCode.Should().Be(HttpStatusCode.Created, await payRes.Content.ReadAsStringAsync());
        return (await payRes.Content.ReadFromJsonAsync<Envelope<FnbPaymentInit>>())!.Data;
    }

    [Fact]
    public async Task ATicketPayment_IsConfirmedThroughTheSharedIpnUrl()
    {
        var (_, loungeId) = await VenueAsync();
        int priceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var show = new LoungeShow
            {
                LoungeId = loungeId, Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18], Description = "MLACP-394",
                Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(3)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            var tier = new TicketTier { LoungeShowId = show.Id, Name = "Vào cửa", AccessType = AccessType.Physical, TotalCapacity = 50 };
            db.Add(tier);
            await db.SaveChangesAsync();
            var price = new TicketPrice
            {
                TierId = tier.Id, Name = "Đợt 1", Price = 150_000m, Quota = 50, IsActive = true,
                SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(2),
                PurchaseChannel = PurchaseChannel.Online
            };
            db.Add(price);
            await db.SaveChangesAsync();
            priceId = price.Id;
        }

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var hold = await client.PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = priceId, Quantity = 1 });
        hold.StatusCode.Should().Be(HttpStatusCode.Created, await hold.Content.ReadAsStringAsync());
        var holdId = (await hold.Content.ReadFromJsonAsync<Envelope<HoldData>>())!.Data.HoldId;
        var purchaseRes = await client.PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = holdId });
        purchaseRes.StatusCode.Should().Be(HttpStatusCode.Created, await purchaseRes.Content.ReadAsStringAsync());
        var purchase = (await purchaseRes.Content.ReadFromJsonAsync<Envelope<PurchaseData>>())!.Data;

        (await PaidIpnAsync(purchase.OrderId, purchase.Amount)).RspCode.Should().Be("00");

        using var check = _factory.Services.CreateScope();
        (await Db(check).Payments.AsNoTracking().SingleAsync(p => p.Id == purchase.PaymentId))
            .Status.Should().Be(PaymentStatus.Confirmed);
    }

    [Fact]
    public async Task AnFnbOrderPayment_IsConfirmedThroughTheSharedIpnUrl_ByTheFnbFlow()
    {
        var init = await PendingFnbPaymentAsync();

        (await PaidIpnAsync(init.PaymentGatewayOrderId, init.Amount)).RspCode.Should().Be("00");

        using var check = _factory.Services.CreateScope();
        var db = Db(check);
        (await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == init.PaymentGatewayOrderId))
            .Status.Should().Be(PaymentStatus.Confirmed);
        (await db.LedgerEntries.AsNoTracking()
                .AnyAsync(e => e.ReferenceType == "fnb_order" && e.ReferenceId == init.OrderId.ToString()))
            .Should().BeTrue("only the F&B flow books the order's revenue — a payment marked paid elsewhere books nothing");
    }

    [Fact]
    public async Task ASubscriptionPayment_IsConfirmedThroughTheSharedIpnUrl()
    {
        var (ownerId, _) = await VenueAsync();
        var pkgRes = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            "/api/v1/subscriptions/packages", new
            {
                Name = $"Pkg394-{Guid.NewGuid():N}", Description = "MLACP-394", Price = 300_000m,
                BillingCycle = "Monthly", MaxTicketsPerEvent = 100, HasAiPoster = false, MaxAiPostersPerMonth = 0
            });
        pkgRes.EnsureSuccessStatusCode();
        var packageId = (await pkgRes.Content.ReadFromJsonAsync<Envelope<int>>())!.Data;

        var subscribe = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        subscribe.IsSuccessStatusCode.Should().BeTrue(await subscribe.Content.ReadAsStringAsync());
        string orderId;
        using (var json = JsonDocument.Parse(await subscribe.Content.ReadAsStringAsync()))
            orderId = json.RootElement.GetProperty("data").GetProperty("orderId").GetString()!;
        decimal amount;
        using (var scope = _factory.Services.CreateScope())
            amount = (await Db(scope).Payments.AsNoTracking().SingleAsync(p => p.OrderId == orderId)).GrossAmount;

        (await PaidIpnAsync(orderId, amount)).RspCode.Should().Be("00");

        using var check = _factory.Services.CreateScope();
        var db = Db(check);
        (await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == orderId)).Status.Should().Be(PaymentStatus.Confirmed);
        (await db.OwnerSubscriptions.AsNoTracking().AnyAsync(s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active))
            .Should().BeTrue("the plan the owner paid for must be activated");
    }

    [Fact]
    public async Task ADonation_IsConfirmedThroughTheSharedIpnUrl()
    {
        var (ownerId, loungeId) = await VenueAsync();
        int performanceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var start = DateTimeOffset.UtcNow.AddHours(-1);
            var show = new LoungeShow
            {
                LoungeId = loungeId, Name = $"Donate394-{Guid.NewGuid():N}"[..20], Description = "MLACP-394",
                Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ongoing,
                ScheduledStart = start, ScheduledEnd = start.AddHours(3), VcpmcRoyaltyReference = "VCPMC-TEST"
            };
            var performer = new Performer { Name = $"Artist394-{Guid.NewGuid():N}"[..20], CreatedByUserId = ownerId };
            db.Add(show);
            db.Add(performer);
            await db.SaveChangesAsync();
            var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id };
            db.Add(performance);
            await db.SaveChangesAsync();
            performanceId = performance.Id;
        }

        var donateRes = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience").PostAsJsonAsync(
            "/api/v1/donations", new
            {
                PerformanceId = performanceId, Amount = 100_000m, IsAnonymous = false, Message = "Cảm ơn!", IsMessagePublic = true
            });
        donateRes.StatusCode.Should().Be(HttpStatusCode.Created, await donateRes.Content.ReadAsStringAsync());
        var init = (await donateRes.Content.ReadFromJsonAsync<Envelope<DonationInit>>())!.Data;

        (await PaidIpnAsync(init.OrderId, 100_000m)).RspCode.Should().Be("00");

        using var check = _factory.Services.CreateScope();
        (await Db(check).Donations.AsNoTracking().SingleAsync(d => d.Id == init.DonationId))
            .Status.Should().Be(DonationStatus.PendingOwnerAck, "VNPay confirmed the money; the donation must leave PendingPayment");
    }

    [Fact]
    public async Task AnUnknownOrderReference_IsReportedAsNotFound()
    {
        (await PaidIpnAsync($"ZZ-{Guid.NewGuid():N}", 10_000m)).RspCode.Should().Be("01");
    }

    [Fact]
    public async Task TheTicketHandler_DoesNotConfirmAPaymentThatIsNotForTickets()
    {
        // URL return của vé chỉ gọi đúng handler vé, không qua bộ rẽ nhánh — nơi duy nhất còn đưa được một mã không
        // phải vé vào handler đó.
        var init = await PendingFnbPaymentAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var res = await client.GetAsync($"/api/v1/payments/vnpay/callback?{PaidQuery(init.PaymentGatewayOrderId, init.Amount)}");

        res.StatusCode.Should().Be(HttpStatusCode.Redirect);
        res.Headers.Location!.ToString().Should().Contain("payment/failed");
        using var check = _factory.Services.CreateScope();
        (await Db(check).Payments.AsNoTracking().SingleAsync(p => p.OrderId == init.PaymentGatewayOrderId))
            .Status.Should().Be(PaymentStatus.Pending, "a ticket handler must not mark an F&B payment as paid");
    }
}
