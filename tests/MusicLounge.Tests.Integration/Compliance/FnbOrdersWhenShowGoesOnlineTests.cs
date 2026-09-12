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
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-390. Buổi diễn chuyển sang online (<c>ChangeLoungeShowFormat</c>) thì không còn khán giả tại chỗ: đơn F&amp;B
/// chưa phục vụ gắn với buổi diễn đó bị huỷ, tiền trả trước được hoàn 100%. Món đã mang ra (Served) là hàng đã giao —
/// phòng trà vẫn thu; đơn đã đóng (Paid) không đụng tới. Và không nhận đơn mới gắn với buổi diễn chỉ diễn online hoặc
/// đã bị huỷ. Mỗi bài một phòng trà riêng.
/// </summary>
[Collection("Integration")]
public sealed class FnbOrdersWhenShowGoesOnlineTests
{
    private readonly ApiFactory _factory;

    public FnbOrdersWhenShowGoesOnlineTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int MenuItemId);

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private async Task<Venue> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var owner = new User { Email = $"fnb390-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue390-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        var menu = new FnbMenu { LoungeId = lounge.Id, Name = "Menu", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Add(menu);
        await db.SaveChangesAsync();
        var item = new FnbMenuItem { MenuId = menu.Id, Category = "Drink", Name = "Trà đào", Price = 50_000m, IsAvailable = true };
        db.Add(item);
        await db.SaveChangesAsync();
        return new Venue(owner.Id, lounge.Id, item.Id);
    }

    private async Task<int> BuyerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var user = new User { Email = $"buyer390-{Guid.NewGuid():N}@test.com", FullName = "Khách" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<int> ShowAsync(int loungeId, LoungeShowFormat format = LoungeShowFormat.Offline,
        LoungeShowStatus status = LoungeShowStatus.Published)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var show = new LoungeShow
        {
            LoungeId = loungeId, Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18], Description = "MLACP-390",
            Format = format, Status = status,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(3),
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    /// <param name="prepaid">Khách đã trả trước qua VNPay (Payment Gateway Confirmed) — đơn vẫn ở
    /// <paramref name="status"/> vì bếp chưa phục vụ xong (MLACP-349). Đơn Paid thì có Payment tiền mặt đã đóng.</param>
    private async Task<(int OrderId, int? PaymentId)> OrderAsync(
        Venue venue, int showId, int buyerId, FnbOrderStatus status, bool prepaid = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var order = new FnbOrder
        {
            LoungeId = venue.LoungeId, ShowId = showId, AudienceUserId = buyerId, Status = status,
            PaymentMethod = prepaid ? PaymentMethod.Gateway : PaymentMethod.Cash, TotalAmount = 100_000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(order);
        await db.SaveChangesAsync();
        db.Add(new OrderItem { FnbOrderId = order.Id, MenuItemId = venue.MenuItemId, Quantity = 2, UnitPrice = 50_000m });
        await db.SaveChangesAsync();

        if (!prepaid && status != FnbOrderStatus.Paid) return (order.Id, null);
        var payment = new Payment
        {
            OrderId = $"FNB390-{Guid.NewGuid():N}"[..30], PayerId = buyerId, GrossAmount = 100_000m, NetAmount = 100_000m,
            Method = prepaid ? PaymentMethod.Gateway : PaymentMethod.Cash, Status = PaymentStatus.Confirmed,
            ReferenceType = "FnbOrder", ReferenceId = order.Id.ToString(),
            TransactionId = prepaid ? $"V{Guid.NewGuid():N}"[..16] : null,
            PaidAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();
        return (order.Id, payment.Id);
    }

    private async Task GoOnlineAsync(Venue venue, int showId)
        => (await _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId)
                .PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/format", new { NewFormat = "Online" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task<(FnbOrder Order, List<OrderItem> Items, List<RefundRequest> Refunds)> StateAsync(
        int orderId, int? paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var order = await db.Set<FnbOrder>().AsNoTracking().SingleAsync(o => o.Id == orderId);
        var items = await db.Set<OrderItem>().AsNoTracking().Where(i => i.FnbOrderId == orderId).ToListAsync();
        var refunds = paymentId is null
            ? new List<RefundRequest>()
            : await db.RefundRequests.AsNoTracking().Where(r => r.PaymentId == paymentId).ToListAsync();
        return (order, items, refunds);
    }

    private async Task<List<Notification>> OrderNoticesAsync(int buyerId, int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        return await Db(scope).Notifications.AsNoTracking()
            .Where(n => n.UserId == buyerId && n.Type == NotificationType.FnbOrderUpdate && n.ReferenceId == orderId.ToString())
            .ToListAsync();
    }

    private async Task<HttpResponseMessage> PlaceOrderAsync(Venue venue, int showId)
        => await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience").PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = venue.LoungeId, ShowId = (int?)showId, ZoneId = (int?)null, TableNote = (string?)null,
            PaymentMethod = "Cash", Note = (string?)null,
            Items = new[] { new { MenuItemId = venue.MenuItemId, Quantity = 1, Note = (string?)null } }
        });

    // ── Đơn đang có khi buổi diễn chuyển sang online ─────────────────────────

    [Fact]
    public async Task GoingOnline_AnUnpaidOrderNotYetServed_IsCancelled_AndTheGuestIsTold()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId);
        var (orderId, _) = await OrderAsync(venue, showId, buyer, FnbOrderStatus.Pending);

        await GoOnlineAsync(venue, showId);

        var (order, items, _) = await StateAsync(orderId, null);
        order.Status.Should().Be(FnbOrderStatus.Cancelled, "no one will be at the venue to serve");
        items.Should().OnlyContain(i => i.Cancelled);
        (await OrderNoticesAsync(buyer, orderId)).Should().Contain(n => n.Body.Contains("chuyển sang online"));
    }

    [Fact]
    public async Task GoingOnline_APrepaidOrderStillInTheKitchen_IsCancelled_AndRefundedInFull()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId);
        var (orderId, paymentId) = await OrderAsync(venue, showId, buyer, FnbOrderStatus.Preparing, prepaid: true);

        await GoOnlineAsync(venue, showId);

        var (order, _, refunds) = await StateAsync(orderId, paymentId);
        order.Status.Should().Be(FnbOrderStatus.Cancelled);
        var refund = refunds.Should().ContainSingle().Subject;
        refund.RefundPercentage.Should().Be(100m);
        refund.AmountRequested.Should().Be(100_000m);
        refund.RequestedBy.Should().Be(buyer);
        (await OrderNoticesAsync(buyer, orderId)).Should().Contain(n => n.Body.Contains("hoàn 100%"));
    }

    [Fact]
    public async Task GoingOnline_AnOrderAlreadyServed_IsLeftForTheVenueToCollect()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId);
        var (orderId, _) = await OrderAsync(venue, showId, buyer, FnbOrderStatus.Served);

        await GoOnlineAsync(venue, showId);

        var (order, items, _) = await StateAsync(orderId, null);
        order.Status.Should().Be(FnbOrderStatus.Served, "the food was delivered; the venue can still collect for it");
        items.Should().OnlyContain(i => !i.Cancelled);
    }

    [Fact]
    public async Task GoingOnline_APaidOrder_IsLeftAlone()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId);
        var (orderId, paymentId) = await OrderAsync(venue, showId, buyer, FnbOrderStatus.Paid);

        await GoOnlineAsync(venue, showId);

        var (order, _, refunds) = await StateAsync(orderId, paymentId);
        order.Status.Should().Be(FnbOrderStatus.Paid);
        refunds.Should().BeEmpty();
    }

    [Fact]
    public async Task GoingOnline_OrdersOfAnotherShowAtTheSameVenue_AreUntouched()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId);
        var otherShowId = await ShowAsync(venue.LoungeId);
        var (otherOrderId, _) = await OrderAsync(venue, otherShowId, buyer, FnbOrderStatus.Pending);

        await GoOnlineAsync(venue, showId);

        (await StateAsync(otherOrderId, null)).Order.Status.Should().Be(FnbOrderStatus.Pending);
    }

    /// <summary>Đường huỷ buổi diễn dùng chung phần huỷ đơn đã tách ra — hành vi MLACP-380 phải giữ nguyên.</summary>
    [Fact]
    public async Task CancellingTheShow_StillCancelsAServedUnpaidOrder_AsBefore()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId);
        var (orderId, _) = await OrderAsync(venue, showId, buyer, FnbOrderStatus.Served);

        (await _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId)
                .PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await StateAsync(orderId, null)).Order.Status.Should().Be(FnbOrderStatus.Cancelled);
        (await OrderNoticesAsync(buyer, orderId)).Should().Contain(n => n.Body.Contains("buổi diễn bị huỷ"));
    }

    // ── Đơn mới gắn với buổi diễn ────────────────────────────────────────────

    [Theory]
    [InlineData(LoungeShowFormat.Online, LoungeShowStatus.Published)]
    [InlineData(LoungeShowFormat.Offline, LoungeShowStatus.Cancelled)]
    public async Task ANewOrder_ForAShowWithNoOneAtTheVenue_IsRefused(LoungeShowFormat format, LoungeShowStatus status)
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue.LoungeId, format, status);

        var res = await PlaceOrderAsync(venue, showId);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("không có khán giả tại chỗ");
    }

    [Fact]
    public async Task ANewOrder_ForAnInPersonShow_IsTakenAsBefore()
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue.LoungeId, LoungeShowFormat.Hybrid);

        (await PlaceOrderAsync(venue, showId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
