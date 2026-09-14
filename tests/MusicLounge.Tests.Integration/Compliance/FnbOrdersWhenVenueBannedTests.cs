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
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-393. Lệnh khoá vĩnh viễn có hiệu lực (<c>ApplyDuePenaltiesJob</c>) đã huỷ các buổi diễn sắp tới và đơn F&amp;B
/// gắn với chúng (MLACP-373/380), nhưng đơn F&amp;B KHÔNG gắn buổi diễn bị huỷ — đặt ngoài giờ diễn (ShowId null) —
/// thì giữ nguyên: phòng trà không còn phục vụ trên nền tảng, đơn đang chờ/đang làm không bao giờ được làm, tiền khách
/// trả trước treo. Nay: huỷ đơn Pending/Preparing, hoàn 100% phần trả trước. Món đã mang ra (Served) giữ để phòng trà
/// thu; đơn đã đóng không đụng tới; tạm khoá (có ngày mở lại) không đụng tới. Mỗi bài một phòng trà riêng.
/// </summary>
[Collection("Integration")]
public sealed class FnbOrdersWhenVenueBannedTests
{
    private readonly ApiFactory _factory;

    public FnbOrdersWhenVenueBannedTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int MenuItemId);

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private async Task<Venue> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var owner = new User { Email = $"fnb393-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue393-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
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
        var user = new User { Email = $"buyer393-{Guid.NewGuid():N}@test.com", FullName = "Khách" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Đơn đặt ngoài giờ diễn (ShowId null). <paramref name="prepaid"/>: khách đã trả trước qua VNPay; đơn Paid
    /// thì có Payment tiền mặt đã đóng.</summary>
    private async Task<(int OrderId, int? PaymentId)> OrderAsync(
        Venue venue, int buyerId, FnbOrderStatus status, bool prepaid = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var order = new FnbOrder
        {
            LoungeId = venue.LoungeId, ShowId = null, AudienceUserId = buyerId, Status = status,
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
            OrderId = $"FNB393-{Guid.NewGuid():N}"[..30], PayerId = buyerId, GrossAmount = 100_000m, NetAmount = 100_000m,
            Method = prepaid ? PaymentMethod.Gateway : PaymentMethod.Cash, Status = PaymentStatus.Confirmed,
            ReferenceType = "FnbOrder", ReferenceId = order.Id.ToString(),
            TransactionId = prepaid ? $"V{Guid.NewGuid():N}"[..16] : null,
            PaidAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();
        return (order.Id, payment.Id);
    }

    private async Task PenaltyTakesEffectAsync(Venue venue, PenaltyType type)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            db.VenuePenalties.Add(new VenuePenalty
            {
                LoungeId = venue.LoungeId, PenaltyType = type, Reason = "Vi phạm nghiêm trọng",
                SuspensionDays = type == PenaltyType.Suspension ? 3 : null,
                IssuedBy = SeedHelper.AdminId, IssuedAt = DateTimeOffset.UtcNow.AddDays(-8),
                EffectiveAt = DateTimeOffset.UtcNow.AddMinutes(-1), Status = PenaltyStatus.Active
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplyDuePenaltiesJob>()
                .ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<(FnbOrder Order, List<RefundRequest> Refunds)> StateAsync(int orderId, int? paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var order = await db.Set<FnbOrder>().AsNoTracking().SingleAsync(o => o.Id == orderId);
        var refunds = paymentId is null
            ? new List<RefundRequest>()
            : await db.RefundRequests.AsNoTracking().Where(r => r.PaymentId == paymentId).ToListAsync();
        return (order, refunds);
    }

    private async Task<List<Notification>> NoticesAsync(int userId, NotificationType type)
    {
        using var scope = _factory.Services.CreateScope();
        return await Db(scope).Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.Type == type).ToListAsync();
    }

    [Fact]
    public async Task Ban_APrepaidOrderNotYetServed_IsCancelled_AndRefundedInFull()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var (orderId, paymentId) = await OrderAsync(venue, buyer, FnbOrderStatus.Preparing, prepaid: true);

        await PenaltyTakesEffectAsync(venue, PenaltyType.Ban);

        var (order, refunds) = await StateAsync(orderId, paymentId);
        order.Status.Should().Be(FnbOrderStatus.Cancelled, "the venue no longer serves anyone on the platform");
        var refund = refunds.Should().ContainSingle().Subject;
        refund.RefundPercentage.Should().Be(100m);
        refund.RequestedBy.Should().Be(buyer);
        (await NoticesAsync(buyer, NotificationType.FnbOrderUpdate)).Should().Contain(n =>
            n.ReferenceId == orderId.ToString() && n.Body.Contains("ngừng hoạt động") && n.Body.Contains("hoàn 100%"));
    }

    [Fact]
    public async Task Ban_AnUnpaidOrder_IsLeftForTheVenueToClose()
    {
        // Không có tiền của khách trên nền tảng, và khoá không chặn nhân viên đóng đơn (thu tiền mặt hoặc tự huỷ).
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var (orderId, _) = await OrderAsync(venue, buyer, FnbOrderStatus.Preparing);

        await PenaltyTakesEffectAsync(venue, PenaltyType.Ban);

        (await StateAsync(orderId, null)).Order.Status.Should().Be(FnbOrderStatus.Preparing,
            "cancelling on the venue's behalf protects no buyer money and may void a table being served");
    }

    [Fact]
    public async Task Ban_AServedOrder_IsKeptForTheVenueToCollect()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var (orderId, _) = await OrderAsync(venue, buyer, FnbOrderStatus.Served, prepaid: true);

        await PenaltyTakesEffectAsync(venue, PenaltyType.Ban);

        (await StateAsync(orderId, null)).Order.Status.Should().Be(FnbOrderStatus.Served,
            "the food was delivered before the ban took effect");
    }

    [Fact]
    public async Task Ban_APaidOrder_IsLeftAlone()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var (orderId, paymentId) = await OrderAsync(venue, buyer, FnbOrderStatus.Paid);

        await PenaltyTakesEffectAsync(venue, PenaltyType.Ban);

        var (order, refunds) = await StateAsync(orderId, paymentId);
        order.Status.Should().Be(FnbOrderStatus.Paid);
        refunds.Should().BeEmpty();
    }

    [Fact]
    public async Task Suspension_LeavesOpenOrdersAlone()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        var (orderId, _) = await OrderAsync(venue, buyer, FnbOrderStatus.Pending, prepaid: true);

        await PenaltyTakesEffectAsync(venue, PenaltyType.Suspension);

        (await StateAsync(orderId, null)).Order.Status.Should().Be(FnbOrderStatus.Pending,
            "a suspension has an end date — the venue comes back");
    }

    [Fact]
    public async Task Ban_OrdersOfAnotherVenue_AreUntouched()
    {
        var banned = await VenueAsync();
        var other = await VenueAsync();
        var buyer = await BuyerAsync();
        var (otherOrderId, _) = await OrderAsync(other, buyer, FnbOrderStatus.Pending, prepaid: true);

        await PenaltyTakesEffectAsync(banned, PenaltyType.Ban);

        (await StateAsync(otherOrderId, null)).Order.Status.Should().Be(FnbOrderStatus.Pending);
    }

    [Fact]
    public async Task Ban_TheOwnerIsToldTheOpenOrdersWereCancelled()
    {
        var venue = await VenueAsync();
        var buyer = await BuyerAsync();
        await OrderAsync(venue, buyer, FnbOrderStatus.Pending, prepaid: true);

        await PenaltyTakesEffectAsync(venue, PenaltyType.Ban);

        (await NoticesAsync(venue.OwnerId, NotificationType.PenaltyIssued))
            .Should().Contain(n => n.Body.Contains("đã trả trước mà chưa phục vụ"));
    }
}
