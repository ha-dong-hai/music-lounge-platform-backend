using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF5;

/// <summary>
/// MLACP-630. Khán giả gọi món bằng điện thoại: đơn phải tự mang KHU khách đang ngồi, lấy từ vé vào cửa của chính họ
/// (lúc mua vé họ chỉ chọn khu). Trước task này <c>FnbOrder.ZoneId</c> không ai gửi và không DTO nào trả.
///
/// Mỗi ca tự dựng khu + buổi diễn + vé riêng và XOÁ trong finally — seed dùng chung giữa các test, một buổi "đang diễn"
/// bỏ lại ở phòng trà seed sẽ làm lệch các test khác.
/// </summary>
[Collection("Integration")]
public sealed class FnbGuestSeatTests
{
    private readonly ApiFactory _factory;

    public FnbGuestSeatTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Seat(Guid ShowId, string ShowName, Guid ZoneId, string ZoneName);
    private sealed record OrderView(Guid Id, string? TableNote, Guid? ZoneId, string? ZoneName, Guid? ShowId);
    private sealed record OrderPage(List<OrderView> Items, int TotalCount);

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
    private HttpClient Staff() => _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

    private sealed record Setup(Guid ZoneId, Guid ShowId, Guid TierId, Guid PriceId, Guid TicketId, Guid MenuId, Guid ItemId);

    /// <summary>Một khu, một buổi diễn tại chỗ, một vé của khán giả seed ở khu đó, và một món để gọi.</summary>
    private async Task<Setup> ArrangeAsync(
        LoungeShowStatus showStatus, DateTimeOffset start, TicketStatus ticketStatus, string zoneName = "Khu Giữa",
        Guid? buyerId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var zone = new SeatingZone { LoungeId = SeedHelper.LoungeId, Name = zoneName, Capacity = 40 };
        db.Add(zone);
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId, Name = $"Đêm 630-{Guid.NewGuid():N}"[..20], Description = "MLACP-630",
            Format = LoungeShowFormat.Offline, Status = showStatus,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Phổ thông", AccessType = AccessType.Physical, TotalCapacity = 40, ZoneId = zone.Id
        };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Giá chuẩn", Price = 150_000m, Quota = 40, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-2), PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();
        var ticket = new Ticket
        {
            BuyerId = buyerId ?? SeedHelper.AudienceId, PriceId = price.Id, TierId = tier.Id, ShowId = show.Id,
            Status = ticketStatus, QrCode = Guid.NewGuid().ToString("N"), PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(ticket);

        var menu = new FnbMenu { LoungeId = SeedHelper.LoungeId, Name = "Menu MLACP-630", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Add(menu);
        await db.SaveChangesAsync();
        var item = new FnbMenuItem { MenuId = menu.Id, Category = "Drink", Name = "Trà sen", Price = 45_000m, IsAvailable = true };
        db.Add(item);
        await db.SaveChangesAsync();

        return new Setup(zone.Id, show.Id, tier.Id, price.Id, ticket.Id, menu.Id, item.Id);
    }

    private async Task CleanupAsync(Setup s)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var orderIds = await db.Set<OrderItem>().Where(i => i.MenuItemId == s.ItemId).Select(i => i.FnbOrderId).ToListAsync();
        db.RemoveRange(db.Set<OrderItem>().Where(i => i.MenuItemId == s.ItemId));
        await db.SaveChangesAsync();
        db.RemoveRange(db.Set<FnbOrder>().Where(o => orderIds.Contains(o.Id)));
        db.RemoveRange(db.Set<Ticket>().Where(t => t.Id == s.TicketId));
        await db.SaveChangesAsync();
        db.RemoveRange(db.Set<FnbMenuItem>().Where(i => i.Id == s.ItemId));
        db.RemoveRange(db.Set<TicketPrice>().Where(p => p.Id == s.PriceId));
        await db.SaveChangesAsync();
        db.RemoveRange(db.Set<FnbMenu>().Where(m => m.Id == s.MenuId));
        db.RemoveRange(db.Set<TicketTier>().Where(t => t.Id == s.TierId));
        await db.SaveChangesAsync();
        db.RemoveRange(db.LoungeShows.Where(x => x.Id == s.ShowId));
        db.RemoveRange(db.Set<SeatingZone>().Where(z => z.Id == s.ZoneId));
        await db.SaveChangesAsync();
    }

    private async Task<Seat?> MySeatAsync()
    {
        var res = await Audience().GetAsync($"/api/v1/fnb-orders/my-seat?loungeId={SeedHelper.LoungeId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<Seat?>>())!.Data;
    }

    private async Task<Guid> OrderAsync(HttpClient client, Guid itemId, Guid? zoneId = null)
    {
        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId, ShowId = (Guid?)null, ZoneId = zoneId, TableNote = "Bàn sát cột",
            PaymentMethod = "Cash", Note = (string?)null,
            Items = new[] { new { MenuItemId = itemId, Quantity = 1, Note = (string?)null } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<Envelope<Guid>>())!.Data;
    }

    private async Task<OrderView> StaffSeesAsync(Guid orderId)
    {
        var res = await Staff().GetAsync($"/api/v1/fnb-orders?loungeId={SeedHelper.LoungeId}&pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<OrderPage>>())!.Data.Items.Single(o => o.Id == orderId);
    }

    [Fact]
    public async Task KhachCoVeBuoiDangDien_DonTuMangKhuTheoVe_VaNhanVienThayTenKhu()
    {
        var s = await ArrangeAsync(LoungeShowStatus.Ongoing, DateTimeOffset.UtcNow.AddMinutes(-30), TicketStatus.Confirmed);
        try
        {
            var seat = await MySeatAsync();
            seat.Should().NotBeNull();
            seat!.ZoneId.Should().Be(s.ZoneId);
            seat.ZoneName.Should().Be("Khu Giữa");
            seat.ShowId.Should().Be(s.ShowId);

            var orderId = await OrderAsync(Audience(), s.ItemId); // client KHÔNG gửi khu

            var order = await StaffSeesAsync(orderId);
            order.ZoneId.Should().Be(s.ZoneId, "khu lấy từ vé của khách, không phụ thuộc client có gửi hay không");
            order.ZoneName.Should().Be("Khu Giữa");
            order.TableNote.Should().Be("Bàn sát cột");
            order.ShowId.Should().BeNull("gắn đơn với buổi diễn là quyết định về tiền (huỷ/hoàn theo buổi), không tự gán");
        }
        finally { await CleanupAsync(s); }
    }

    [Fact]
    public async Task VeDaSoat_VanTinh_VaBuoiSapMoCuaTrongBaGio_CungTinh()
    {
        var s = await ArrangeAsync(LoungeShowStatus.Published, DateTimeOffset.UtcNow.AddHours(2), TicketStatus.Used);
        try { (await MySeatAsync())!.ZoneId.Should().Be(s.ZoneId); }
        finally { await CleanupAsync(s); }
    }

    [Theory]
    [InlineData(TicketStatus.Cancelled)]
    [InlineData(TicketStatus.Refunded)]
    [InlineData(TicketStatus.Pending)]
    public async Task VeKhongConHieuLuc_KhongSuyRaKhu_DonKhongMangKhu(TicketStatus status)
    {
        var s = await ArrangeAsync(LoungeShowStatus.Ongoing, DateTimeOffset.UtcNow.AddMinutes(-30), status);
        try
        {
            (await MySeatAsync()).Should().BeNull();
            (await StaffSeesAsync(await OrderAsync(Audience(), s.ItemId))).ZoneId.Should().BeNull();
        }
        finally { await CleanupAsync(s); }
    }

    [Fact]
    public async Task VeCuaBuoiConXa_KhongTinh()
    {
        var s = await ArrangeAsync(LoungeShowStatus.Published, DateTimeOffset.UtcNow.AddDays(5), TicketStatus.Confirmed);
        try { (await MySeatAsync()).Should().BeNull("khách chưa ở phòng trà — vé của một đêm khác không nói gì về chỗ đang ngồi"); }
        finally { await CleanupAsync(s); }
    }

    [Fact]
    public async Task VeCuaBuoiDaKetThuc_KhongTinh()
    {
        var s = await ArrangeAsync(LoungeShowStatus.Ended, DateTimeOffset.UtcNow.AddHours(-5), TicketStatus.Used);
        try { (await MySeatAsync()).Should().BeNull(); }
        finally { await CleanupAsync(s); }
    }

    [Fact]
    public async Task ClientGuiKhuKhac_ThiTheoClient_KhachCoTheDaDoiCho()
    {
        var s = await ArrangeAsync(LoungeShowStatus.Ongoing, DateTimeOffset.UtcNow.AddMinutes(-30), TicketStatus.Confirmed);
        Guid otherZone;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var z = new SeatingZone { LoungeId = SeedHelper.LoungeId, Name = "Khu Ban công", Capacity = 10 };
            db.Add(z);
            await db.SaveChangesAsync();
            otherZone = z.Id;
        }
        try
        {
            var order = await StaffSeesAsync(await OrderAsync(Audience(), s.ItemId, otherZone));
            order.ZoneId.Should().Be(otherZone);
            order.ZoneName.Should().Be("Khu Ban công");
        }
        finally
        {
            await CleanupAsync(s);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.RemoveRange(db.Set<SeatingZone>().Where(z => z.Id == otherZone));
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task NhanVienTaoDonHoKhach_KhongSuyKhuTuVe()
    {
        // Vé đứng tên CHÍNH nhân viên: nếu handler quên kiểm vai thì đơn tạo hộ khách sẽ mang khu của nhân viên.
        var s = await ArrangeAsync(LoungeShowStatus.Ongoing, DateTimeOffset.UtcNow.AddMinutes(-30), TicketStatus.Confirmed,
            buyerId: SeedHelper.StaffId);
        try { (await StaffSeesAsync(await OrderAsync(Staff(), s.ItemId))).ZoneId.Should().BeNull(); }
        finally { await CleanupAsync(s); }
    }
}
