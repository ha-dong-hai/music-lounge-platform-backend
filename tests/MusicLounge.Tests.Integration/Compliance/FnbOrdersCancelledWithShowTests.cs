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
/// MLACP-380, sửa ở MLACP-632 (chủ phòng trà huỷ buổi thì đơn đồ uống giữ nguyên). <c>ShowCancellation.CancelAsync</c> (MLACP-373) hoàn 100% mọi vé khi một buổi diễn bị huỷ — chủ động
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

    private sealed record Venue(Guid OwnerId, Guid LoungeId);

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

    private async Task<Guid> BuyerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User { Email = $"buyer380-{Guid.NewGuid():N}@test.com", FullName = "Khách" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> ShowAsync(Guid loungeId, DateTimeOffset start)
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

    private async Task<Guid> MenuItemAsync(Guid loungeId)
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
    private async Task<(Guid OrderId, Guid? PaymentId)> FnbOrderAsync(
        Guid loungeId, Guid showId, Guid menuItemId, Guid? audienceUserId, FnbOrderStatus status,
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

        Guid? paymentId = null;
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

    private async Task<FnbOrder> OrderStateAsync(Guid orderId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<FnbOrder>().AsNoTracking()
            .SingleAsync(o => o.Id == orderId);
    }

    private async Task<List<OrderItem>> ItemsAsync(Guid orderId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<OrderItem>().AsNoTracking()
            .Where(i => i.FnbOrderId == orderId).ToListAsync();
    }

    private async Task<List<RefundRequest>> RefundsAsync(Guid paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefundRequests.AsNoTracking()
            .Where(r => r.PaymentId == paymentId).ToListAsync();
    }

    private async Task<List<Notification>> NoticesAsync(Guid userId, NotificationType type)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.Type == type).ToListAsync();
    }

    // ── Huỷ show do chủ phòng trà chủ động — MLACP-632: đơn đồ uống GIỮ NGUYÊN, khách chỉ được báo ─────────

    private Task<HttpResponseMessage> OwnerCancelsAsync(Venue venue, Guid showId)
        => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId)
            .PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null);

    /// <summary>Vé vào cửa của khách cho buổi diễn — để biết họ là khách của buổi đó khi đơn đặt qua app không mang ShowId.</summary>
    private async Task TicketAsync(Guid showId, Guid buyer)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tier = new TicketTier { LoungeShowId = showId, Name = "Phổ thông", AccessType = AccessType.Physical, TotalCapacity = 10 };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Giá chuẩn", Price = 150_000m, Quota = 10, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();
        db.Add(new Ticket
        {
            BuyerId = buyer, PriceId = price.Id, TierId = tier.Id, ShowId = showId, Status = TicketStatus.Confirmed,
            QrCode = Guid.NewGuid().ToString("N"), PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task OwnerCancelsShow_UnpaidOrderStillWaiting_IsKept_AndCustomerToldTheyCanCancelThemselves()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var menuItemId = await MenuItemAsync(venue.LoungeId);
        var (orderId, _) = await FnbOrderAsync(venue.LoungeId, showId, menuItemId, buyer, FnbOrderStatus.Pending);

        (await OwnerCancelsAsync(venue, showId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OrderStateAsync(orderId)).Status.Should().Be(FnbOrderStatus.Pending, "khách tự quyết, hệ thống không huỷ thay");
        (await ItemsAsync(orderId)).Should().OnlyContain(i => !i.Cancelled);
        (await NoticesAsync(buyer, NotificationType.FnbOrderUpdate)).Should().Contain(n =>
            n.ReferenceId == orderId.ToString() && n.Body.Contains("vẫn được giữ") && n.Body.Contains("tự huỷ"));
    }

    [Fact]
    public async Task OwnerCancelsShow_PrepaidOrderBeingMade_IsKept_NoAutomaticRefund_CustomerToldToTalkToStaff()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var menuItemId = await MenuItemAsync(venue.LoungeId);
        var (orderId, paymentId) = await FnbOrderAsync(
            venue.LoungeId, showId, menuItemId, buyer, FnbOrderStatus.Preparing, gatewayConfirmed: true);

        (await OwnerCancelsAsync(venue, showId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OrderStateAsync(orderId)).Status.Should().Be(FnbOrderStatus.Preparing);
        (await RefundsAsync(paymentId!.Value)).Should().BeEmpty("không tự hoàn — đơn vẫn được giữ");
        (await NoticesAsync(buyer, NotificationType.FnbOrderUpdate)).Should().Contain(n =>
            n.ReferenceId == orderId.ToString() && n.Body.Contains("trao đổi với nhân viên"));
    }

    [Fact]
    public async Task OwnerCancelsShow_AppOrderWithoutShow_OfATicketHolder_IsKept_AndCustomerIsTold()
    {
        // Đơn khách đặt qua app không mang ShowId — vẫn phải báo nếu khách có vé buổi bị huỷ.
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var stranger = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        await TicketAsync(showId, buyer);
        var menuItemId = await MenuItemAsync(venue.LoungeId);
        var (orderId, _) = await FnbOrderAsync(venue.LoungeId, showId, menuItemId, buyer, FnbOrderStatus.Pending);
        var (strangerOrder, _) = await FnbOrderAsync(venue.LoungeId, showId, menuItemId, stranger, FnbOrderStatus.Pending);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var o in await db.Set<FnbOrder>().Where(o => o.Id == orderId || o.Id == strangerOrder).ToListAsync())
                o.ShowId = null;
            await db.SaveChangesAsync();
        }

        (await OwnerCancelsAsync(venue, showId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OrderStateAsync(orderId)).Status.Should().Be(FnbOrderStatus.Pending);
        (await NoticesAsync(buyer, NotificationType.FnbOrderUpdate))
            .Should().Contain(n => n.ReferenceId == orderId.ToString() && n.Body.Contains("vẫn được giữ"));
        (await NoticesAsync(stranger, NotificationType.FnbOrderUpdate))
            .Should().BeEmpty("khách không có vé buổi này và đơn không gắn buổi này — không liên quan");
    }

    [Fact]
    public async Task OwnerCancelsShow_AlreadyPaidFnbOrder_IsLeftAlone()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var showId = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var menuItemId = await MenuItemAsync(venue.LoungeId);
        var (orderId, paymentId) = await FnbOrderAsync(venue.LoungeId, showId, menuItemId, buyer, FnbOrderStatus.Paid);

        (await OwnerCancelsAsync(venue, showId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OrderStateAsync(orderId)).Status.Should().Be(FnbOrderStatus.Paid);
        (await RefundsAsync(paymentId!.Value)).Should().BeEmpty();
        (await NoticesAsync(buyer, NotificationType.FnbOrderUpdate)).Should().BeEmpty("đơn đã xong, không có gì để báo");
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
