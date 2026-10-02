using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF5;

/// <summary>
/// MLACP-357. <c>GET /fnb-orders</c> chỉ dành cho chủ phòng trà và nhân viên — khán giả không có cách
/// nào xem đơn F&amp;B của chính mình, kể cả đơn đã trả tiền online; họ chỉ biết tình trạng đơn qua
/// thông báo. Sau MLACP-349 "đã trả tiền" tách khỏi bước của bếp (<c>IsPaid</c>), và sau MLACP-351 đơn
/// có thể bị huỷ kèm hoàn tiền: khách cần nhìn thấy được cả hai.
/// </summary>
[Collection("Integration")]
public sealed class MyFnbOrdersTests
{
    private const decimal ItemPrice = 35_000m;

    private readonly ApiFactory _factory;

    public MyFnbOrdersTests(ApiFactory factory) => _factory = factory;

    private sealed record DataResponse<T>(bool Success, T Data);
    private sealed record PaymentInit(Guid OrderId, string PaymentGatewayOrderId, decimal Amount, string PaymentUrl);
    private sealed record OrderView(Guid Id, Guid? AudienceUserId, string Status, bool IsPaid, decimal TotalAmount);
    private sealed record OrderPage(List<OrderView> Items, int TotalCount);

    private async Task<Guid> MenuItemAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var menu = new FnbMenu
        {
            LoungeId = SeedHelper.LoungeId, Name = "Menu MLACP-357", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Add(menu);
        await db.SaveChangesAsync();
        var item = new FnbMenuItem
        {
            MenuId = menu.Id, Category = "Drink", Name = "Cà phê muối", Price = ItemPrice, IsAvailable = true
        };
        db.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<Guid> PlaceOrderAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId,
            ShowId = (Guid?)null,
            ZoneId = (Guid?)null,
            TableNote = "Bàn E5",
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = await MenuItemAsync(), Quantity = 1, Note = (string?)null } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DataResponse<Guid>>())!.Data;
    }

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
    private HttpClient OtherAudience() => _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Audience");
    private HttpClient Staff() => _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

    private async Task<OrderPage> MyOrdersAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/v1/fnb-orders/my?pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<DataResponse<OrderPage>>())!.Data;
    }

    [Fact]
    public async Task KhachThayDonCuaMinhVaBietDaTraTienChua()
    {
        var older = await PlaceOrderAsync(Audience());
        var newer = await PlaceOrderAsync(Audience());

        // Trả online cho đơn mới hơn.
        var pay = await Audience().PostAsync($"/api/v1/fnb-orders/{newer}/pay", null);
        var txnRef = (await pay.Content.ReadFromJsonAsync<DataResponse<PaymentInit>>())!.Data.PaymentGatewayOrderId;
        (await _factory.CreateClient().GetAsync(
                $"/api/v1/fnb-orders/vnpay-ipn?vnp_TxnRef={txnRef}&vnp_ResponseCode=00" +
                $"&vnp_Amount={(long)(ItemPrice * 100)}&vnp_TransactionNo={$"M{Guid.NewGuid():N}"[..14]}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var mine = (await MyOrdersAsync(Audience())).Items;

        var newerView = mine.Should().ContainSingle(o => o.Id == newer).Subject;
        newerView.IsPaid.Should().BeTrue("khách vừa trả online — phải thấy điều đó mà không cần chờ thông báo");
        newerView.Status.Should().Be("Pending", "bếp chưa làm gì — trả tiền không phải là phục vụ (MLACP-349)");
        mine.Should().ContainSingle(o => o.Id == older).Which.IsPaid.Should().BeFalse();

        mine.FindIndex(o => o.Id == newer).Should().BeLessThan(mine.FindIndex(o => o.Id == older),
            "đơn mới nhất lên trước");
    }

    [Fact]
    public async Task KhongThayDonCuaNguoiKhac()
    {
        var mineId = await PlaceOrderAsync(Audience());
        var othersId = await PlaceOrderAsync(OtherAudience());

        var mine = (await MyOrdersAsync(Audience())).Items;

        mine.Should().Contain(o => o.Id == mineId);
        mine.Should().NotContain(o => o.Id == othersId);
        mine.Should().OnlyContain(o => o.AudienceUserId == SeedHelper.AudienceId);
    }

    [Fact]
    public async Task DonNhanVienTaoChoKhachVangLaiKhongNamTrongDanhSachCuaAi()
    {
        // Đơn nhân viên tạo hộ khách không có tài khoản thì không có AudienceUserId.
        var staffOrder = await PlaceOrderAsync(Staff());

        (await MyOrdersAsync(Audience())).Items.Should().NotContain(o => o.Id == staffOrder);
    }

    [Fact]
    public async Task ChuaDangNhapThiKhongXemDuoc()
    {
        (await _factory.CreateClient().GetAsync("/api/v1/fnb-orders/my")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    // MLACP-500. Khán giả mới (để đếm chính xác) có 15 đơn ở phòng trà A rồi 3 đơn ở phòng trà B. Đơn ở B được tạo TRƯỚC,
    // nên không lọc thì trang 1 (10 đơn mới nhất) chỉ toàn đơn của A — đúng tình huống làm mất nút Trả online.
    private async Task<(Guid UserId, List<Guid> PhongB)> KhachCoDonOHaiPhongTraAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var khach = new User { Email = $"f500-{Guid.NewGuid():N}@test.com", FullName = "Khach 500" };
        db.Users.Add(khach);
        await db.SaveChangesAsync();

        List<FnbOrder> Don(Guid loungeId, int soLuong) => Enumerable.Range(0, soLuong).Select(_ => new FnbOrder
        {
            LoungeId = loungeId, AudienceUserId = khach.Id, PaymentMethod = PaymentMethod.Cash, TotalAmount = ItemPrice
        }).ToList();

        var phongB = Don(SeedHelper.OtherLoungeId, 3);
        db.AddRange(phongB);
        await db.SaveChangesAsync();
        db.AddRange(Don(SeedHelper.LoungeId, 15));
        await db.SaveChangesAsync();
        return (khach.Id, phongB.Select(o => o.Id).ToList());
    }

    [Fact]
    public async Task LocTheoPhongTra_TraDuDonCuaPhongDoDuBiDonPhongKhacDayKhoiTrang1()
    {
        var (userId, phongB) = await KhachCoDonOHaiPhongTraAsync();
        var khach = _factory.CreateAuthenticatedClient(userId, "Audience");

        var res = await khach.GetAsync($"/api/v1/fnb-orders/my?loungeId={SeedHelper.OtherLoungeId}&pageSize=10");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await res.Content.ReadFromJsonAsync<DataResponse<OrderPage>>())!.Data;
        page.Items.Select(o => o.Id).Should().BeEquivalentTo(phongB);
        page.TotalCount.Should().Be(3, "totalCount đếm theo phòng trà đã lọc");
    }

    [Fact]
    public async Task KhongTruyenLoungeId_TraNhuCu_DonCuaMoiPhongTra()
    {
        var (userId, _) = await KhachCoDonOHaiPhongTraAsync();
        var khach = _factory.CreateAuthenticatedClient(userId, "Audience");

        var page = (await (await khach.GetAsync("/api/v1/fnb-orders/my?pageSize=10"))
            .Content.ReadFromJsonAsync<DataResponse<OrderPage>>())!.Data;

        page.TotalCount.Should().Be(18);
        page.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task HangDoiCuaPhongTra_TraDonMoiNhatTruoc()
    {
        // MLACP-411: chu thich cu ghi "theo thu tu tao" nhung API tra moi nhat truoc; client doc chu thich da hien nguoc.
        // Khoa thu tu nay: chu phong tra xem lich su order can trang 1 la don gan day, khong phai don tu nhieu thang truoc.
        var first = await PlaceOrderAsync(Staff());
        var second = await PlaceOrderAsync(Staff());
        var third = await PlaceOrderAsync(Staff());

        var res = await Staff().GetAsync($"/api/v1/fnb-orders?loungeId={SeedHelper.LoungeId}&pageSize=2");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await res.Content.ReadFromJsonAsync<DataResponse<OrderPage>>())!.Data;
        page.Items.Select(o => o.Id).Should().Equal(third, second);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(3);
        first.CompareTo(second).Should().BeNegative();
    }
}
