using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF5;

/// <summary>
/// MLACP-350. Tiền F&amp;B khách trả online vào tài khoản merchant VNPay của nền tảng. Trước task này
/// IPN ghi sổ cái Có thẳng vào tài khoản User của chủ phòng trà và không tạo <c>Settlement</c> nào —
/// không có chỉ dẫn chi trả nào đi theo, và khoản đó không hiện ở màn thu nhập của phòng trà.
///
/// <para>Nay đi đúng khuôn của vé: giữ ở Platform, lên lịch một khoản 100% khi đơn đóng (đã phục vụ
/// và đã trả), giữ thêm 48 giờ, rồi giải ngân qua <c>SettlementReleaseJob</c>.</para>
/// </summary>
[Collection("Integration")]
public sealed class FnbSettlementTests
{
    private const decimal ItemPrice = 75_000m;
    private const int Quantity = 2;
    private const decimal OrderTotal = ItemPrice * Quantity;

    private readonly ApiFactory _factory;

    public FnbSettlementTests(ApiFactory factory) => _factory = factory;

    private sealed record DataResponse<T>(bool Success, T Data);
    private sealed record PaymentInit(int OrderId, string PaymentGatewayOrderId, decimal Amount, string PaymentUrl);
    private sealed record IpnBody(string RspCode, string Message);
    private sealed record RecentSettlement(int Id, decimal NetAmount, string Status);
    private sealed record Earnings(List<RecentSettlement> RecentSettlements);
    private sealed record RevenueReportSlice(decimal TotalSettlementReceived);

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
    private HttpClient Staff() => _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

    private async Task<int> CreateOrderAsync()
    {
        int menuItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var menu = new FnbMenu
            {
                LoungeId = SeedHelper.LoungeId, Name = "Menu MLACP-350", IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Add(menu);
            await db.SaveChangesAsync();
            var item = new FnbMenuItem
            {
                MenuId = menu.Id, Category = "Food", Name = "Khô gà", Price = ItemPrice, IsAvailable = true
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
            TableNote = "Bàn C2",
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity, Note = (string?)null } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;
    }

    private async Task<string> InitiateAsync(int orderId)
    {
        var res = await Audience().PostAsync($"/api/v1/fnb-orders/{orderId}/pay", null);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DataResponse<PaymentInit>>())!.Data.PaymentGatewayOrderId;
    }

    private async Task<string> IpnAsync(string txnRef)
    {
        var transactionNo = $"S{Guid.NewGuid():N}"[..14];
        var url = $"/api/v1/fnb-orders/vnpay-ipn?vnp_TxnRef={txnRef}&vnp_ResponseCode=00" +
                  $"&vnp_Amount={(long)(OrderTotal * 100)}&vnp_TransactionNo={transactionNo}";
        var res = await _factory.CreateClient().GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<IpnBody>())!.RspCode;
    }

    private async Task StaffSetAsync(int orderId, string status)
    {
        var res = await Staff().PutAsJsonAsync($"/api/v1/fnb-orders/{orderId}/status", new { Status = status });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<List<Payment>> PaymentsAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Payments.AsNoTracking()
            .Where(p => p.ReferenceType == "FnbOrder" && p.ReferenceId == orderId.ToString())
            .ToListAsync();
    }

    private async Task<List<Settlement>> SettlementsAsync(int orderId)
    {
        var paymentIds = (await PaymentsAsync(orderId)).Select(p => p.Id).ToList();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Settlements.AsNoTracking().Where(s => paymentIds.Contains(s.PaymentId)).ToListAsync();
    }

    private async Task<List<(AccountType Type, int? OwnerId, decimal Amount, bool IsDebit)>> PurchaseLinesAsync(int orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.ReferenceType == "fnb_order" && e.ReferenceId == orderId.ToString())
            .Select(e => new { e.Account.OwnerType, e.Account.OwnerId, e.Amount, e.IsDebit })
            .ToListAsync();
        return rows.Select(r => (r.OwnerType, r.OwnerId, r.Amount, r.IsDebit)).ToList();
    }

    private async Task<int> PaidAndServedAsync()
    {
        var orderId = await CreateOrderAsync();
        (await IpnAsync(await InitiateAsync(orderId))).Should().Be("00");
        await StaffSetAsync(orderId, "Preparing");
        await StaffSetAsync(orderId, "Served");
        return orderId;
    }

    private async Task ReleaseDueAsync(int settlementId)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settlement = await db.Settlements.SingleAsync(s => s.Id == settlementId);
            settlement.ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        using var jobScope = _factory.Services.CreateScope();
        await jobScope.ServiceProvider.GetRequiredService<SettlementReleaseJob>()
            .ExecuteAsync(new JobCancellationToken(false));
    }

    // ── Tiền giữ ở Platform, chưa chi khi món chưa giao ─────────────────────

    [Fact]
    public async Task TraOnlineThiTienGiuOPlatformVaChuaChiKhiMonChuaPhucVu()
    {
        var orderId = await CreateOrderAsync();
        (await IpnAsync(await InitiateAsync(orderId))).Should().Be("00");

        var lines = await PurchaseLinesAsync(orderId);
        lines.Should().ContainSingle(l => l.Type == AccountType.Platform && !l.IsDebit && l.Amount == OrderTotal,
            "tiền của phòng trà được giữ hộ ở Platform cho tới khi giải ngân — đúng khuôn của vé");
        lines.Should().NotContain(l => l.Type == AccountType.User,
            "trước đây ghi Có thẳng cho chủ phòng trà mà không có khoản chi trả nào đi theo");

        (await SettlementsAsync(orderId)).Should().BeEmpty("món chưa được phục vụ — chưa có gì để trả cho phòng trà");
    }

    // ── Đơn đóng thì lên lịch một khoản 100% ─────────────────────────────────

    [Fact]
    public async Task PhucVuXongThiLenLichMotKhoanDayDuSauThoiGianGiu()
    {
        var before = DateTimeOffset.UtcNow;
        var orderId = await PaidAndServedAsync();

        var settlement = (await SettlementsAsync(orderId)).Should().ContainSingle().Subject;
        settlement.ReleaseType.Should().Be(SettlementReleaseType.Full);
        settlement.GrossAmount.Should().Be(OrderTotal);
        settlement.NetAmount.Should().Be(OrderTotal, "F&B không thu hoa hồng");
        settlement.OwnerId.Should().Be(SeedHelper.OwnerId);
        settlement.BankAccountId.Should().NotBeNull("phòng trà trong seed có tài khoản nhận tiền mặc định");
        settlement.Status.Should().Be(SettlementStatus.Scheduled);
        settlement.ScheduledAt.Should().BeOnOrAfter(before.AddHours(48))
            .And.BeOnOrBefore(DateTimeOffset.UtcNow.AddHours(48),
                "giữ 48 giờ sau khi đơn đóng — còn hoàn được nếu món có vấn đề");
    }

    [Fact]
    public async Task TraSauKhiDaPhucVuThiLenLichNgayLucTienVe()
    {
        var orderId = await CreateOrderAsync();
        await StaffSetAsync(orderId, "Preparing");
        await StaffSetAsync(orderId, "Served");

        (await IpnAsync(await InitiateAsync(orderId))).Should().Be("00");

        (await SettlementsAsync(orderId)).Should().ContainSingle(
            "đơn đã phục vụ xong nên IPN đóng đơn — và đó là lúc lên lịch trả cho phòng trà");
    }

    [Fact]
    public async Task DenHanThiGiaiNganChoPhongTraVaHienOManThuNhap()
    {
        var orderId = await PaidAndServedAsync();
        var settlement = (await SettlementsAsync(orderId)).Single();

        await ReleaseDueAsync(settlement.Id);

        (await SettlementsAsync(orderId)).Single().Status.Should().Be(SettlementStatus.Released);

        var res = await Owner().GetAsync("/api/v1/me/earnings");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var earnings = (await res.Content.ReadFromJsonAsync<DataResponse<Earnings>>())!.Data;
        earnings.RecentSettlements.Should().Contain(s => s.Id == settlement.Id && s.Status == "Released",
            "trước đây khoản này không bao giờ xuất hiện ở màn thu nhập của phòng trà");
    }

    // ── Không trả cho phòng trà thứ nền tảng chưa từng giữ ──────────────────

    [Fact]
    public async Task DonTraTienMatKhongCoKhoanChiTra()
    {
        var orderId = await CreateOrderAsync();
        await StaffSetAsync(orderId, "Preparing");
        await StaffSetAsync(orderId, "Served");
        await StaffSetAsync(orderId, "Paid");

        (await SettlementsAsync(orderId)).Should().BeEmpty(
            "phòng trà cầm tiền mặt từ tay khách — nền tảng chưa bao giờ giữ đồng nào để chi");
    }

    [Fact]
    public async Task KhoanTraTrungKhongDuocChiChoPhongTra()
    {
        var orderId = await CreateOrderAsync();
        var first = await InitiateAsync(orderId);
        var second = await InitiateAsync(orderId);
        (await IpnAsync(first)).Should().Be("00");
        (await IpnAsync(second)).Should().Be("02");
        await StaffSetAsync(orderId, "Preparing");
        await StaffSetAsync(orderId, "Served");

        var settlement = (await SettlementsAsync(orderId)).Should().ContainSingle().Subject;
        var applied = (await PaymentsAsync(orderId)).Single(p => p.Status == PaymentStatus.Confirmed);
        settlement.PaymentId.Should().Be(applied.Id,
            "khoản trả trùng không được áp vào đơn — tiền đó là của khách, không phải của phòng trà");
    }

    [Fact]
    public async Task ThanhToanGhiSoKieuCuKhongBiGhiCoChoChuPhongTraLanHai()
    {
        // Thanh toán xác nhận trước MLACP-350: bút toán đã ghi Có thẳng cho chủ phòng trà. Lên lịch chi
        // trả cho nó nghĩa là ghi Có họ lần hai trong khi Platform chưa từng giữ khoản đó.
        var orderId = await CreateOrderAsync();
        await StaffSetAsync(orderId, "Preparing");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();
            var payment = new Payment
            {
                OrderId = $"FNB-LEGACY-{Guid.NewGuid():N}"[..40],
                PayerId = SeedHelper.AudienceId,
                GrossAmount = OrderTotal,
                NetAmount = OrderTotal,
                Method = PaymentMethod.Gateway,
                Status = PaymentStatus.Confirmed,
                TransactionId = $"L{Guid.NewGuid():N}"[..14],
                ReferenceType = "FnbOrder",
                ReferenceId = orderId.ToString(),
                PaidAt = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
            };
            db.Add(payment);
            await db.SaveChangesAsync();

            await ledger.WriteJournalAsync(
                Guid.NewGuid().ToString("N"), "fnb_order", orderId.ToString(), payment.Id,
                new LedgerLine[]
                {
                    new(AccountType.Gateway, null, OrderTotal, IsDebit: true),
                    new(AccountType.User, SeedHelper.OwnerId, OrderTotal, IsDebit: false)
                });
            await db.SaveChangesAsync();
        }

        await StaffSetAsync(orderId, "Served");

        (await SettlementsAsync(orderId)).Should().BeEmpty();
    }

    // ── Báo cáo đối soát ────────────────────────────────────────────────────

    [Fact]
    public async Task BaoCaoDoiSoatTinhCaKhoanF_BDaNhan()
    {
        var orderId = await PaidAndServedAsync();
        var settlement = (await SettlementsAsync(orderId)).Single();

        var before = await SettlementReceivedAsync();
        await ReleaseDueAsync(settlement.Id);
        var after = await SettlementReceivedAsync();

        (after - before).Should().Be(OrderTotal,
            "\"đã nhận quyết toán\" phải khớp tiền phòng trà thật sự nhận được, kể cả tiền F&B");
    }

    private async Task<decimal> SettlementReceivedAsync()
    {
        var res = await Owner().GetAsync($"/api/v1/analytics/revenue-report?loungeId={SeedHelper.LoungeId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<DataResponse<RevenueReportSlice>>())!.Data.TotalSettlementReceived;
    }
}
