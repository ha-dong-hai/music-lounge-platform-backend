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
/// MLACP-349. Luồng thanh toán online F&amp;B trước task này <b>chưa có một bài test nào</b>. Đọc code
/// thấy ba lỗi, cả ba đều do "đã trả tiền" và "bếp đã làm tới đâu" dùng chung <c>FnbOrder.Status</c>:
/// <list type="bullet">
///   <item>IPN chống trùng theo từng giao dịch chứ không theo đơn — đơn đã trả vẫn bị trừ tiền lần
///   hai, đơn đã huỷ mà tiền về muộn thì bị hồi sinh thành Paid.</item>
///   <item>IPN nhảy thẳng Pending → Paid, mà sau Paid không còn bước nào — bếp kẹt với đơn trả trước.</item>
///   <item>Bấm "thanh toán" đã đổi phương thức sang Gateway trước khi có đồng nào — trả tiền mặt sau
///   đó thì bản ghi tiền mặt mang Method = Gateway.</item>
/// </list>
/// </summary>
[Collection("Integration")]
public sealed class FnbOnlinePaymentTests
{
    private const decimal ItemPrice = 60_000m;
    private const int Quantity = 2;
    private const decimal OrderTotal = ItemPrice * Quantity;

    private readonly ApiFactory _factory;

    public FnbOnlinePaymentTests(ApiFactory factory) => _factory = factory;

    private sealed record DataResponse<T>(bool Success, T Data);
    private sealed record PaymentInit(int OrderId, string PaymentGatewayOrderId, decimal Amount, string PaymentUrl);
    private sealed record IpnBody(string RspCode, string Message);
    private sealed record OrderView(int Id, string Status, bool IsPaid, DateTimeOffset? OnlinePaymentLiveUntil);
    private sealed record OrderPage(List<OrderView> Items);
    private sealed record OwnerAnalyticsSlice(decimal FnbRevenue);
    private sealed record RevenueReportSlice(decimal TotalFnbRevenue);

    // ── Dựng dữ liệu qua đúng các endpoint thật ─────────────────────────────

    private async Task<int> CreateOrderAsync()
    {
        int menuItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var menu = new FnbMenu
            {
                LoungeId = SeedHelper.LoungeId, Name = "Menu MLACP-349", IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Add(menu);
            await db.SaveChangesAsync();

            var item = new FnbMenuItem
            {
                MenuId = menu.Id, Category = "Drink", Name = "Trà đào", Price = ItemPrice, IsAvailable = true
            };
            db.Add(item);
            await db.SaveChangesAsync();
            menuItemId = item.Id;
        }

        var res = await Audience().PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = "Bàn A1",
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity, Note = (string?)null } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;
    }

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
    private HttpClient Staff() => _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

    private async Task<string> InitiateAsync(int orderId)
    {
        var res = await Audience().PostAsync($"/api/v1/fnb-orders/{orderId}/pay", null);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DataResponse<PaymentInit>>())!.Data.PaymentGatewayOrderId;
    }

    private async Task<string> IpnAsync(string txnRef, string transactionNo, string responseCode = "00")
    {
        var url = $"/api/v1/fnb-orders/vnpay-ipn?vnp_TxnRef={txnRef}&vnp_ResponseCode={responseCode}" +
                  $"&vnp_Amount={(long)(OrderTotal * 100)}&vnp_TransactionNo={transactionNo}";
        var res = await _factory.CreateClient().GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<IpnBody>())!.RspCode;
    }

    private Task<HttpResponseMessage> StaffSetAsync(int orderId, string status)
        => Staff().PutAsJsonAsync($"/api/v1/fnb-orders/{orderId}/status", new { Status = status });

    private static string NewTransactionNo() => $"T{Guid.NewGuid():N}"[..14];

    private async Task<FnbOrder> OrderAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.FnbOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
    }

    private async Task<List<Payment>> PaymentsAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Payments.AsNoTracking()
            .Where(p => p.ReferenceType == "FnbOrder" && p.ReferenceId == orderId.ToString())
            .ToListAsync();
    }

    /// <summary>Lùi thời điểm tạo giao dịch — để link VNPay của nó đã hết hạn (15 phút).</summary>
    private async Task AgePaymentAsync(string txnRef, int minutes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db.Payments.SingleAsync(p => p.OrderId == txnRef);
        payment.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-minutes);
        await db.SaveChangesAsync();
    }

    private async Task<OrderView> OrderViewAsync(int orderId)
    {
        var res = await Owner().GetAsync($"/api/v1/fnb-orders?loungeId={SeedHelper.LoungeId}&pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await res.Content.ReadFromJsonAsync<DataResponse<OrderPage>>())!.Data;
        return page.Items.Single(o => o.Id == orderId);
    }

    // ── Bếp vẫn đi tiếp được với đơn trả trước ──────────────────────────────

    [Fact]
    public async Task DonTraTruocVanDiTiepQuaBepVaTuDongDongKhiPhucVuXong()
    {
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);

        (await IpnAsync(txnRef, NewTransactionNo())).Should().Be("00");

        var afterPayment = await OrderAsync(orderId);
        afterPayment.Status.Should().Be(FnbOrderStatus.Pending,
            "bếp chưa làm gì cả — trả tiền không phải là phục vụ");
        afterPayment.PaymentMethod.Should().Be(PaymentMethod.Gateway);

        (await StaffSetAsync(orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.NoContent,
            "trước đây IPN đã nhảy đơn sang Paid, và sau Paid không còn bước nào");
        (await StaffSetAsync(orderId, "Served")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OrderAsync(orderId)).Status.Should().Be(FnbOrderStatus.Paid,
            "đã trả tiền và đã phục vụ — không còn gì để làm, đóng đơn");
        (await PaymentsAsync(orderId)).Count(p => p.Status == PaymentStatus.Confirmed).Should().Be(1,
            "đóng đơn đã trả online không được ghi thêm một khoản thu tiền mặt");
    }

    // ── Chốt ở tầng ĐƠN: không trả trùng, không hồi sinh ────────────────────

    [Fact]
    public async Task GiaoDichThuHaiChoDonDaTraKhongDuocGhiVaoDon()
    {
        var orderId = await CreateOrderAsync();
        var first = await InitiateAsync(orderId);
        var second = await InitiateAsync(orderId); // khách bấm "thanh toán" lần nữa khi link cũ còn hạn

        (await IpnAsync(first, NewTransactionNo())).Should().Be("00");
        var secondTransactionNo = NewTransactionNo();
        (await IpnAsync(second, secondTransactionNo)).Should().Be("02",
            "đơn đã được trả — VNPay yêu cầu kiểm trạng thái ĐƠN, không chỉ trạng thái giao dịch");

        var payments = await PaymentsAsync(orderId);
        payments.Count(p => p.Status == PaymentStatus.Confirmed).Should().Be(1);
        var duplicate = payments.Single(p => p.OrderId == second);
        duplicate.Status.Should().Be(PaymentStatus.Failed, "không áp vào đơn");
        duplicate.TransactionId.Should().Be(secondTransactionNo,
            "tiền đã rời tài khoản khách — mã giao dịch phải được giữ để đối soát và hoàn");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.LedgerEntries
                .Where(e => e.ReferenceType == "fnb_order" && e.ReferenceId == orderId.ToString())
                .Select(e => e.JournalId).Distinct().CountAsync())
            .Should().Be(1, "sổ cái chỉ được ghi một lần cho một đơn");

        (await db.Notifications.CountAsync(n =>
                n.UserId == SeedHelper.AdminId
                && n.Type == NotificationType.PaymentConfirmedAfterExpiry
                && n.ReferenceType == "payment" && n.ReferenceId == duplicate.Id.ToString()))
            .Should().Be(1, "Admin phải biết có một khoản tiền đã thu mà không gắn với đơn nào");
        (await db.Notifications.AnyAsync(n =>
                n.UserId == SeedHelper.AudienceId
                && n.Title == "Giao dịch không được ghi vào đơn"
                && n.ReferenceId == orderId.ToString()))
            .Should().BeTrue("người trả tiền phải được nói thẳng chuyện gì đã xảy ra");
    }

    [Fact]
    public async Task CallbackLapLaiCuaKhoanTraTrungKhongBaoLanHai()
    {
        var orderId = await CreateOrderAsync();
        var first = await InitiateAsync(orderId);
        var second = await InitiateAsync(orderId);
        await IpnAsync(first, NewTransactionNo());

        var transactionNo = NewTransactionNo();
        (await IpnAsync(second, transactionNo)).Should().Be("02");
        (await IpnAsync(second, transactionNo)).Should().Be("02", "VNPay gọi lại tối đa 10 lần");

        var duplicateId = (await PaymentsAsync(orderId)).Single(p => p.OrderId == second).Id.ToString();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.CountAsync(n =>
                n.UserId == SeedHelper.AdminId
                && n.Type == NotificationType.PaymentConfirmedAfterExpiry
                && n.ReferenceId == duplicateId))
            .Should().Be(1);
    }

    [Fact]
    public async Task DonDaHuyMaTienVeMuonKhongBiHoiSinh()
    {
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);
        await AgePaymentAsync(txnRef, minutes: 20); // link đã hết hạn nên nhân viên huỷ được

        (await StaffSetAsync(orderId, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await IpnAsync(txnRef, NewTransactionNo())).Should().Be("02");

        (await OrderAsync(orderId)).Status.Should().Be(FnbOrderStatus.Cancelled,
            "trước đây IPN đặt Paid vô điều kiện — một đơn đã huỷ sống lại");
    }

    [Fact]
    public async Task TienVeMuonChoDonConMoVanDuocGhiNhan()
    {
        // Job dọn thanh toán bỏ dở đã đánh Failed, rồi xác nhận thành công mới về. Với vé thì từ chối
        // (chỗ ngồi có thể đã bán cho người khác); đơn F&B còn mở thì không mất gì — từ chối nghĩa là
        // khách đã trả tiền mà đơn vẫn hiện chưa thanh toán.
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = await db.Payments.SingleAsync(p => p.OrderId == txnRef);
            payment.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync();
        }

        (await IpnAsync(txnRef, NewTransactionNo())).Should().Be("00");

        (await PaymentsAsync(orderId)).Single().Status.Should().Be(PaymentStatus.Confirmed);
    }

    [Fact]
    public async Task KhongTaoDuocLinkThanhToanChoDonDaTraHoacDaHuy()
    {
        var paidOrder = await CreateOrderAsync();
        await IpnAsync(await InitiateAsync(paidOrder), NewTransactionNo());

        (await Audience().PostAsync($"/api/v1/fnb-orders/{paidOrder}/pay", null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict,
                "trước đây chỉ soi Status == Paid, mà đơn trả trước nay vẫn ở Pending");

        var cancelledOrder = await CreateOrderAsync();
        (await StaffSetAsync(cancelledOrder, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Audience().PostAsync($"/api/v1/fnb-orders/{cancelledOrder}/pay", null)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Tiền mặt và huỷ trong lúc khách đang trả online ─────────────────────

    [Fact]
    public async Task KhongThuTienMatKhiKhachDangTraOnline()
    {
        var orderId = await CreateOrderAsync();
        await InitiateAsync(orderId);
        (await StaffSetAsync(orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StaffSetAsync(orderId, "Served")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var res = await StaffSetAsync(orderId, "Paid");

        res.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "khách hoàn tất link VNPay sau khi đã đưa tiền mặt thì bị trừ hai lần");
        (await res.Content.ReadAsStringAsync()).Should().Contain("đang thanh toán online");
        (await OrderAsync(orderId)).Status.Should().Be(FnbOrderStatus.Served);
    }

    [Fact]
    public async Task LinkHetHanThiThuTienMatDuocVaGhiDungPhuongThuc()
    {
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);
        await AgePaymentAsync(txnRef, minutes: 20);

        (await StaffSetAsync(orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StaffSetAsync(orderId, "Served")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StaffSetAsync(orderId, "Paid")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cash = (await PaymentsAsync(orderId)).Single(p => p.Status == PaymentStatus.Confirmed);
        cash.Method.Should().Be(PaymentMethod.Cash,
            "trước đây bấm 'thanh toán' đã đổi phương thức sang Gateway, và khoản tiền mặt mang theo nó");
        (await OrderAsync(orderId)).PaymentMethod.Should().Be(PaymentMethod.Cash);
    }

    [Fact]
    public async Task KhongHuyKhiKhachDangTraOnline()
    {
        var orderId = await CreateOrderAsync();
        await InitiateAsync(orderId);

        (await StaffSetAsync(orderId, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "huỷ lúc này thì khách vẫn trả được tiền cho một đơn đã huỷ");
    }

    // MLACP-351: bài "không huỷ ngang đơn khách đã trả online" (422) đã được thay — chốt đó chỉ đứng tạm
    // vì lúc ấy chưa có đường hoàn tiền F&B. Nay huỷ được, kèm yêu cầu hoàn 100%: xem FnbRefundTests.

    // ── Minh bạch: màn hình nhân viên và báo cáo doanh thu ──────────────────

    [Fact]
    public async Task DanhSachDonChoThayDaTraChuaVaKhachCoDangTraOnlineKhong()
    {
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);

        var paying = await OrderViewAsync(orderId);
        paying.IsPaid.Should().BeFalse();
        paying.OnlinePaymentLiveUntil.Should().NotBeNull("nhân viên phải thấy vì sao hệ thống không cho thu tiền mặt");

        await IpnAsync(txnRef, NewTransactionNo());

        var paid = await OrderViewAsync(orderId);
        paid.Status.Should().Be("Pending");
        paid.IsPaid.Should().BeTrue("Status không còn trả lời được câu 'khách đã trả chưa'");
    }

    [Fact]
    public async Task DoanhThuTinhCaDonTraTruocChuaPhucVu()
    {
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);

        var before = await FnbRevenueAsync();
        await IpnAsync(txnRef, NewTransactionNo());
        var after = await FnbRevenueAsync();

        (after - before).Should().Be(OrderTotal,
            "tiền đã về từ lúc IPN xác nhận — bếp chưa phục vụ không làm khoản đó bớt thật");
    }

    [Fact]
    public async Task BaoCaoDoiSoatDoanhThuCungTinhDonTraTruocChuaPhucVu()
    {
        // Báo cáo đối soát dùng đường tính riêng (OwnerRevenueReportBuilder) — sửa một chỗ mà quên chỗ
        // kia thì hai màn hình của cùng một chủ phòng trà nói hai con số khác nhau.
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);

        var before = await ReportFnbRevenueAsync();
        await IpnAsync(txnRef, NewTransactionNo());
        var after = await ReportFnbRevenueAsync();

        (after - before).Should().Be(OrderTotal);
    }

    private async Task<decimal> ReportFnbRevenueAsync()
    {
        var res = await Owner().GetAsync($"/api/v1/analytics/revenue-report?loungeId={SeedHelper.LoungeId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<DataResponse<RevenueReportSlice>>())!.Data.TotalFnbRevenue;
    }

    private async Task<decimal> FnbRevenueAsync()
    {
        var res = await Owner().GetAsync($"/api/v1/analytics/my-lounge?loungeId={SeedHelper.LoungeId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<DataResponse<OwnerAnalyticsSlice>>())!.Data.FnbRevenue;
    }
}
