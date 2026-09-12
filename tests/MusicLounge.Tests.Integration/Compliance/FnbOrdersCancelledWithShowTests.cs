using System.Net;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-380. <c>ShowCancellation.CancelAsync</c> (MLACP-373) hoàn 100% mọi vé khi một buổi diễn bị huỷ — chủ động
/// bởi chủ phòng trà (<c>CancelLoungeShowCommandHandler</c>) hay tự động khi phòng trà bị khoá/tạm khoá
/// (<c>ApplyDuePenaltiesJob</c>) — nhưng trước task này không đụng gì tới các <see cref="FnbOrder"/> gắn với show đó
/// (ShowId). Đơn khách đã đặt/đã trả trước cho một buổi diễn không còn tổ chức treo nguyên, tiền trả trước không ai
/// hoàn.
///
/// <para>Đơn đã đóng (<see cref="FnbOrderStatus.Paid"/>) là giao dịch đã xong — không đụng tới, dù đóng bằng tiền
/// mặt hay online, giữ đúng ranh giới <c>UpdateFnbOrderStatusCommandHandler</c> đã đặt cho việc huỷ 1 đơn F&amp;B.</para>
/// </summary>
[Collection("Integration")]
public sealed class FnbOrdersCancelledWithShowTests
{
    private readonly ApiFactory _factory;

    public FnbOrdersCancelledWithShowTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId);

    private async Task<Venue> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"fnb380-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue380-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return new Venue(owner.Id, lounge.Id);
    }

    private async Task<int> BuyerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User { Email = $"buyer380-{Guid.NewGuid():N}@test.com", FullName = "Khách" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<int> ShowAsync(int loungeId, DateTimeOffset start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18],
            Description = "MLACP-380",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(3),
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<int> MenuItemAsync(int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var menu = new FnbMenu { LoungeId = loungeId, Name = "Menu", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Add(menu);
        await db.SaveChangesAsync();
        var item = new FnbMenuItem
        {
            MenuId = menu.Id, Category = "Drink", Name = "Trà đào", Price = 50_000m, IsAvailable = true
        };
        db.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    /// <param name="gatewayConfirmed">Có một Payment Gateway Confirmed cho đơn này (khách đã trả trước qua VNPay,
    /// bếp chưa phục vụ xong nên đơn vẫn ở <paramref name="status"/>, không nhảy Paid — đúng quy tắc MLACP-349).</param>
    private async Task<(int OrderId, int? PaymentId)> FnbOrderAsync(
        int loungeId, int showId, int menuItemId, int? audienceUserId, FnbOrderStatus status,
        bool gatewayConfirmed = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var order = new FnbOrder
        {
            LoungeId = loungeId, ShowId = showId, AudienceUserId = audienceUserId,
            Status = status, PaymentMethod = gatewayConfirmed ? PaymentMethod.Gateway : PaymentMethod.Cash,
            TotalAmount = 100_000m, CreatedAt = DateTime.UtcNow
        };
        db.Add(order);
        await db.SaveChangesAsync();

        db.Add(new OrderItem
        {
            FnbOrderId = order.Id, MenuItemId = menuItemId, Quantity = 2, UnitPrice = 50_000m, Cancelled = false
        });
        await db.SaveChangesAsync();

        int? paymentId = null;
        if (gatewayConfirmed)
        {
            var payment = new Payment
            {
                OrderId = $"FNB380-{Guid.NewGuid():N}"[..30],
                PayerId = audienceUserId,
                GrossAmount = 100_000m,
                NetAmount = 100_000m,
                Method = PaymentMethod.Gateway,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "FnbOrder",
                ReferenceId = order.Id.ToString(),
                TransactionId = $"V{Guid.NewGuid():N}"[..16],
                PaidAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }
        else if (status == FnbOrderStatus.Paid)
        {
            // Da dong bang tien mat — giao dich da xong, khong lien quan Gateway.
            var payment = new Payment
            {
                OrderId = $"FNB380-{Guid.NewGuid():N}"[..30],
                PayerId = audienceUserId,
                GrossAmount = 100_000m,
                NetAmount = 100_000m,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "FnbOrder",
                ReferenceId = order.Id.ToString(),
                PaidAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }

        return (order.Id, paymentId);
    }

    private async Task<FnbOrder> OrderStateAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<FnbOrder>().AsNoTracking()
            .SingleAsync(o => o.Id == orderId);
    }

    private async Task<List<OrderItem>> ItemsAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<OrderItem>().AsNoTracking()
            .Where(i => i.FnbOrderId == orderId).ToListAsync();
    }

    private async Task<List<RefundRequest>> RefundsAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefundRequests.AsNoTracking()
            .Where(r => r.PaymentId == paymentId).ToListAsync();
    }

    private async Task<List<Notification>> NoticesAsync(int userId, NotificationType type)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.Type == type).ToListAsync();
    }

    // ── Huỷ show do chủ phòng trà chủ động ───────────────────────────────────

    [Fact]
    public async Task OwnerCancelsShow_UnpaidFnbOrder_IsCancelledOutright_NoRefundNeeded()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var menuItemId = await MenuItemAsync(venue.LoungeId);
        var (orderId, _) = await FnbOrderAsync(venue.LoungeId, showId, menuItemId, buyer, FnbOrderStatus.Pending);

        var client = _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);
        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OrderStateAsync(orderId)).Status.Should().Be(FnbOrderStatus.Cancelled);
        (await ItemsAsync(orderId)).Should().OnlyContain(i => i.Cancelled);
        (await NoticesAsync(buyer, NotificationType.FnbOrderUpdate))
            .Should().Contain(n => n.ReferenceId == orderId.ToString() && n.Body.Contains("buổi diễn bị huỷ"));
    }

    [Fact]
    public async Task OwnerCancelsShow_PrepaidFnbOrderNotYetServed_IsCancelled_AndRefunded100Percent()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var menuItemId = await MenuItemAsync(venue.LoungeId);
        var (orderId, paymentId) = await FnbOrderAsync(
            venue.LoungeId, showId, menuItemId, buyer, FnbOrderStatus.Preparing, gatewayConfirmed: true);

        var client = _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);
        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OrderStateAsync(orderId)).Status.Should().Be(FnbOrderStatus.Cancelled);
        (await ItemsAsync(orderId)).Should().OnlyContain(i => i.Cancelled);
        var refund = (await RefundsAsync(paymentId!.Value)).Should().ContainSingle().Subject;
        refund.RefundPercentage.Should().Be(100m);
        refund.AmountRequested.Should().Be(100_000m);
        refund.RequestedBy.Should().Be(buyer);
        (await NoticesAsync(buyer, NotificationType.FnbOrderUpdate))
            .Should().Contain(n => n.ReferenceId == orderId.ToString() && n.Body.Contains("hoàn 100%"));
    }

    [Fact]
    public async Task OwnerCancelsShow_AlreadyPaidFnbOrder_IsLeftAlone()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var menuItemId = await MenuItemAsync(venue.LoungeId);
        var (orderId, paymentId) = await FnbOrderAsync(venue.LoungeId, showId, menuItemId, buyer, FnbOrderStatus.Paid);

        var client = _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);
        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OrderStateAsync(orderId)).Status.Should().Be(FnbOrderStatus.Paid,
            "giao dịch tiền mặt đã xong trước khi show bị huỷ — không liên quan");
        (await ItemsAsync(orderId)).Should().OnlyContain(i => !i.Cancelled);
        (await RefundsAsync(paymentId!.Value)).Should().BeEmpty();
    }

    // ── Huỷ show tự động khi phòng trà bị khoá (ApplyDuePenaltiesJob) ────────

    [Fact]
    public async Task VenueBanned_CancelsUnpaidFnbOrdersOfItsUpcomingShows_TooToo()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var menuItemId = await MenuItemAsync(venue.LoungeId);
        var (orderId, paymentId) = await FnbOrderAsync(
            venue.LoungeId, showId, menuItemId, buyer, FnbOrderStatus.Pending, gatewayConfirmed: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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

        (await OrderStateAsync(orderId)).Status.Should().Be(FnbOrderStatus.Cancelled);
        var refund = (await RefundsAsync(paymentId!.Value)).Should().ContainSingle().Subject;
        refund.RefundPercentage.Should().Be(100m);
        (await NoticesAsync(venue.OwnerId, NotificationType.PenaltyIssued))
            .Should().Contain(n => n.Body.Contains("đơn F&B chưa đóng"), "chủ phòng trà cũng cần biết ban đã đụng tới F&B");
    }
}
