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
/// MLACP-631 — ai được bấm gì trên một đơn gọi món, đối chiếu cách quán thật vận hành:
/// <list type="bullet">
/// <item>Khách tự huỷ được khi quầy CHƯA nhận (ShopeeFood: huỷ được khi "Đang chờ xác nhận").</item>
/// <item>Huỷ phải có lý do (KiotViet, CUKCUK bắt chọn lý do khi huỷ món đã báo bếp).</item>
/// <item>Đơn đã bắt đầu làm chỉ chủ phòng trà huỷ (Toast: huỷ cần quyền riêng / quản lý duyệt).</item>
/// <item>Quản trị viên nền tảng không bấm thay phòng trà.</item>
/// <item>Thu tiền mặt ghi được ai thu — để đối chiếu cuối ca.</item>
/// </list>
/// </summary>
[Collection("Integration")]
public sealed class FnbOrderCancelRulesTests
{
    private const decimal ItemPrice = 55_000m;

    private readonly ApiFactory _factory;

    public FnbOrderCancelRulesTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record PaymentInit(Guid OrderId, string PaymentGatewayOrderId, decimal Amount, string PaymentUrl);
    private sealed record OrderView(Guid Id, string Status, string? CancelReason, DateTimeOffset? CancelledAt,
        string? CancelledByName, string? CashCollectedByName);
    private sealed record OrderPage(List<OrderView> Items, int TotalCount);

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
    private HttpClient OtherAudience() => _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Audience");
    private HttpClient Staff() => _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<Guid> CreateOrderAsync()
    {
        Guid menuItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var menu = new FnbMenu { LoungeId = SeedHelper.LoungeId, Name = "Menu MLACP-631", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Add(menu);
            await db.SaveChangesAsync();
            var item = new FnbMenuItem { MenuId = menu.Id, Category = "Drink", Name = "Trà gừng mật ong", Price = ItemPrice, IsAvailable = true };
            db.Add(item);
            await db.SaveChangesAsync();
            menuItemId = item.Id;
        }
        var res = await Audience().PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId, ShowId = (Guid?)null, ZoneId = (Guid?)null, TableNote = "Bàn B2",
            PaymentMethod = "Cash", Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity = 1, Note = (string?)null } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<Envelope<Guid>>())!.Data;
    }

    private async Task<string> InitiateAsync(Guid orderId)
    {
        var res = await Audience().PostAsync($"/api/v1/fnb-orders/{orderId}/pay", null);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<Envelope<PaymentInit>>())!.Data.PaymentGatewayOrderId;
    }

    private async Task PayOnlineAsync(Guid orderId)
    {
        var txnRef = await InitiateAsync(orderId);
        var transactionNo = $"C{Guid.NewGuid():N}"[..14];
        var url = $"/api/v1/fnb-orders/vnpay-ipn?vnp_TxnRef={txnRef}&vnp_ResponseCode=00" +
                  $"&vnp_Amount={(long)(ItemPrice * 100)}&vnp_TransactionNo={transactionNo}";
        (await _factory.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> SetAsync(HttpClient c, Guid orderId, string status, string? reason = null)
        => c.PutAsJsonAsync($"/api/v1/fnb-orders/{orderId}/status", new { Status = status, Reason = reason });

    private async Task<OrderView> VenueSeesAsync(Guid orderId, HttpClient? client = null)
    {
        var res = await (client ?? Owner()).GetAsync($"/api/v1/fnb-orders?loungeId={SeedHelper.LoungeId}&pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<OrderPage>>())!.Data.Items.Single(o => o.Id == orderId);
    }

    private async Task<OrderView> CustomerSeesAsync(Guid orderId)
    {
        var res = await Audience().GetAsync("/api/v1/fnb-orders/my?pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<OrderPage>>())!.Data.Items.Single(o => o.Id == orderId);
    }

    private async Task<string> FullNameAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).FullName;
    }

    private async Task<List<RefundRequest>> RefundsForOrderAsync(Guid orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var refId = orderId.ToString();
        var paymentIds = await db.Payments.AsNoTracking()
            .Where(p => p.ReferenceType == "FnbOrder" && p.ReferenceId == refId).Select(p => p.Id).ToListAsync();
        return await db.Set<RefundRequest>().AsNoTracking().Where(r => paymentIds.Contains(r.PaymentId)).ToListAsync();
    }

    // ── Lý do huỷ ───────────────────────────────────────────────────────────

    [Fact]
    public async Task NhanVienHuyKhongGhiLyDo_BiTuChoi_GhiLyDoThiHuyDuoc_VaLuuDauVet()
    {
        var orderId = await CreateOrderAsync();
        (await SetAsync(Staff(), orderId, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SetAsync(Staff(), orderId, "Cancelled", "   ")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await VenueSeesAsync(orderId)).Status.Should().Be("Pending");

        (await SetAsync(Staff(), orderId, "Cancelled", "  Hết trà gừng  ")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var venue = await VenueSeesAsync(orderId);
        venue.Status.Should().Be("Cancelled");
        venue.CancelReason.Should().Be("Hết trà gừng");
        venue.CancelledAt.Should().NotBeNull();
        venue.CancelledByName.Should().Be(await FullNameAsync(SeedHelper.StaffId));

        var customer = await CustomerSeesAsync(orderId);
        customer.CancelReason.Should().Be("Hết trà gừng", "khách được biết vì sao đơn của mình bị huỷ");
        customer.CancelledByName.Should().BeNull("tên nhân viên là thông tin nội bộ của phòng trà");
    }

    // ── Món đã làm: chỉ chủ phòng trà huỷ ──────────────────────────────────

    [Fact]
    public async Task DonDaBatDauLam_NhanVienKhongHuyDuoc_ChuPhongTraHuyDuoc()
    {
        var orderId = await CreateOrderAsync();
        (await SetAsync(Staff(), orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SetAsync(Staff(), orderId, "Cancelled", "Khách đổi ý")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await VenueSeesAsync(orderId)).Status.Should().Be("Preparing");

        (await SetAsync(Owner(), orderId, "Cancelled", "Khách đổi ý sau khi pha")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var v = await VenueSeesAsync(orderId);
        v.Status.Should().Be("Cancelled");
        v.CancelledByName.Should().Be(await FullNameAsync(SeedHelper.OwnerId));
    }

    [Fact]
    public async Task DonDaMangRaBan_NhanVienKhongHuyDuoc()
    {
        var orderId = await CreateOrderAsync();
        (await SetAsync(Staff(), orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SetAsync(Staff(), orderId, "Served")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SetAsync(Staff(), orderId, "Cancelled", "Khách bỏ về")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await VenueSeesAsync(orderId)).Status.Should().Be("Served");
    }

    // ── Quản trị viên nền tảng không bấm thay ───────────────────────────────

    [Fact]
    public async Task QuanTriVienKhongBamThayPhongTra()
    {
        var orderId = await CreateOrderAsync();
        (await SetAsync(Admin(), orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SetAsync(Admin(), orderId, "Cancelled", "Thử")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await VenueSeesAsync(orderId)).Status.Should().Be("Pending");
    }

    // ── Khách tự huỷ ────────────────────────────────────────────────────────

    [Fact]
    public async Task KhachTuHuyKhiQuayChuaNhan_ThiHuyDuoc_VaGhiLaKhachHuy()
    {
        var orderId = await CreateOrderAsync();
        (await Audience().PostAsync($"/api/v1/fnb-orders/{orderId}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var v = await VenueSeesAsync(orderId);
        v.Status.Should().Be("Cancelled");
        v.CancelReason.Should().Be("Khách tự huỷ khi quầy chưa nhận đơn");
        v.CancelledByName.Should().Be(await FullNameAsync(SeedHelper.AudienceId));
        (await RefundsForOrderAsync(orderId)).Should().BeEmpty("đơn chưa trả tiền thì không có gì để hoàn");
    }

    [Fact]
    public async Task KhachKhongTuHuyDuocKhiQuayDaBatDauLam()
    {
        var orderId = await CreateOrderAsync();
        (await SetAsync(Staff(), orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var res = await Audience().PostAsync($"/api/v1/fnb-orders/{orderId}/cancel", null);
        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("nói với nhân viên");
        (await VenueSeesAsync(orderId)).Status.Should().Be("Preparing");
    }

    [Fact]
    public async Task NguoiKhacKhongHuyDuocDonCuaKhach()
    {
        var orderId = await CreateOrderAsync();
        (await OtherAudience().PostAsync($"/api/v1/fnb-orders/{orderId}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await VenueSeesAsync(orderId)).Status.Should().Be("Pending");
    }

    [Fact]
    public async Task KhachDaTraTruocTuHuy_TuTaoYeuCauHoan100()
    {
        var orderId = await CreateOrderAsync();
        await PayOnlineAsync(orderId);
        (await Audience().PostAsync($"/api/v1/fnb-orders/{orderId}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refund = (await RefundsForOrderAsync(orderId)).Single();
        refund.AmountRequested.Should().Be(ItemPrice);
        refund.RefundPercentage.Should().Be(100m);
        refund.Reason.Should().Contain("Khách tự huỷ");
    }

    [Fact]
    public async Task KhachDangThanhToanVnPay_KhongTuHuyDuoc()
    {
        var orderId = await CreateOrderAsync();
        await InitiateAsync(orderId); // link còn hiệu lực, chưa có kết quả
        var res = await Audience().PostAsync($"/api/v1/fnb-orders/{orderId}/cancel", null);
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Bạn đang có một lượt thanh toán",
            "câu nói với khách, không phải câu dành cho nhân viên");
        (await VenueSeesAsync(orderId)).Status.Should().Be("Pending");
    }

    // ── Ai thu tiền mặt ─────────────────────────────────────────────────────

    [Fact]
    public async Task ThuTienMat_GhiNguoiThu_ChiPhongTraThay()
    {
        var orderId = await CreateOrderAsync();
        foreach (var s in new[] { "Preparing", "Served", "Paid" })
            (await SetAsync(Staff(), orderId, s)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await VenueSeesAsync(orderId)).CashCollectedByName.Should().Be(await FullNameAsync(SeedHelper.StaffId));
        (await CustomerSeesAsync(orderId)).CashCollectedByName.Should().BeNull();
    }

    [Fact]
    public async Task DonTraTruocDongKhiPhucVu_KhongGhiNguoiThuTienMat()
    {
        var orderId = await CreateOrderAsync();
        await PayOnlineAsync(orderId);
        (await SetAsync(Staff(), orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SetAsync(Staff(), orderId, "Served")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var v = await VenueSeesAsync(orderId);
        v.Status.Should().Be("Paid");
        v.CashCollectedByName.Should().BeNull("không ai cầm tiền mặt — khách đã trả qua VNPay");
    }
}
