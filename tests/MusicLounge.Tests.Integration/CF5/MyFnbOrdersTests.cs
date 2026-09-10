using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
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
    private sealed record PaymentInit(int OrderId, string PaymentGatewayOrderId, decimal Amount, string PaymentUrl);
    private sealed record OrderView(int Id, int? AudienceUserId, string Status, bool IsPaid, decimal TotalAmount);
    private sealed record OrderPage(List<OrderView> Items, int TotalCount);

    private async Task<int> MenuItemAsync()
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

    private async Task<int> PlaceOrderAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = "Bàn E5",
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = await MenuItemAsync(), Quantity = 1, Note = (string?)null } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;
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
}
