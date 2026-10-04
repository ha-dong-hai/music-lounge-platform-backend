using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-616. Bang dieu khien Admin chi lay thanh toan Confirmed:
///   - hoan MOT PHAN (don nhieu ve, huy mot ve) thi thanh toan van Confirmed — GMV va phan nen tang van tinh du, trong
///     khi so cai da tra phi do lai cho khach;
///   - hoan TOAN BO thi thanh toan thanh Refunded va BIEN MAT khoi thang ban — sua nguoc so lieu cua ky da qua.
/// VAS 14: hang ban bi tra lai ghi giam tru doanh thu o KY PHAT SINH viec tra lai, khong sua ky da ban.
/// Moi ca dung mot thang rieng cua nam 2020 (khong lop test nao khac ghi vao do) — du lieu test dung chung mot DB.
/// </summary>
[Collection("Integration")]
public sealed class AdminDashboardRefundPeriodTests
{
    private readonly ApiFactory _factory;

    public AdminDashboardRefundPeriodTests(ApiFactory factory) => _factory = factory;

    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private static readonly DateTimeOffset NgayBan = new(2020, 1, 10, 20, 0, 0, Vn);
    private static readonly DateTimeOffset NgayHoan = new(2020, 1, 20, 9, 0, 0, Vn);

    private async Task SeedAsync(DateTimeOffset ngayBan, DateTimeOffset ngayHoan)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Payment TT(PaymentStatus st) => new()
        {
            OrderId = $"DBH-{Guid.NewGuid():N}"[..30], GrossAmount = 200_000m, PlatformFee = 10_000m, TaxWithheld = 10_000m,
            NetAmount = 180_000m, Method = PaymentMethod.Gateway, Status = st, TransactionId = $"D{Guid.NewGuid():N}"[..16],
            ReferenceType = "TicketHold", ReferenceId = "0", PaidAt = ngayBan, CreatedAt = ngayBan
        };
        var motPhan = TT(PaymentStatus.Confirmed);   // 2 ve, hoan 1 ve
        var toanBo = TT(PaymentStatus.Refunded);     // 2 ve, hoan ca 2
        db.Payments.AddRange(motPhan, toanBo);
        await db.SaveChangesAsync();

        RefundRequest Hoan(Payment p, decimal soTien) => new()
        {
            PaymentId = p.Id, RequestedBy = SeedHelper.AudienceId, Reason = "Khách huỷ vé",
            AmountRequested = soTien, AmountApproved = soTien, RefundPercentage = 100m,
            Status = RefundRequestStatus.Approved, ResolvedAt = ngayHoan, CreatedAt = ngayHoan.UtcDateTime.AddDays(-1)
        };
        db.RefundRequests.AddRange(Hoan(motPhan, 100_000m), Hoan(toanBo, 200_000m));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TongGmvNenTang_TruTienDaHoan_KeCaHoanMotPhan()
    {
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        async Task<decimal> Gmv() => (await (await admin.GetAsync("/api/v1/analytics/platform"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("totalGrossMerchandiseValue").GetDecimal();

        var truoc = await Gmv();
        // 2 thanh toan 200.000d, da hoan 100.000d + 200.000d — thang 02/2020, tach khoi ca duoi
        await SeedAsync(new DateTimeOffset(2020, 2, 10, 20, 0, 0, Vn), new DateTimeOffset(2020, 2, 20, 9, 0, 0, Vn));
        (await Gmv()).Should().Be(truoc + 100_000m, "400.000đ đã bán trừ 300.000đ đã hoàn");
    }

    [Fact]
    public async Task HoanTien_GhiGiamTruONgayHoan_KhongSuaNgayBan()
    {
        await SeedAsync(NgayBan, NgayHoan);
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var from = Uri.EscapeDataString(new DateTimeOffset(2020, 1, 1, 0, 0, 0, Vn).ToString("o"));
        var to = Uri.EscapeDataString(new DateTimeOffset(2020, 1, 31, 23, 59, 59, Vn).ToString("o"));
        var res = await admin.GetAsync($"/api/v1/analytics/admin-dashboard?from={from}&to={to}");
        res.EnsureSuccessStatusCode();
        var data = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        data.GetProperty("seriesUnit").GetString().Should().Be("day");

        JsonElement Ve(DateTimeOffset ngay) => data.GetProperty("series").EnumerateArray()
            .Single(b => b.GetProperty("start").GetDateTimeOffset().Date == ngay.Date).GetProperty("ticket");

        Ve(NgayBan).GetProperty("gmv").GetDecimal().Should().Be(400_000m,
            "ngày bán giữ nguyên doanh số của cả hai thanh toán, kể cả thanh toán sau này bị hoàn toàn bộ");
        Ve(NgayBan).GetProperty("platformRevenue").GetDecimal().Should().Be(20_000m);
        Ve(NgayHoan).GetProperty("gmv").GetDecimal().Should().Be(-300_000m,
            "300.000đ đã hoàn (100.000 + 200.000) ghi giảm trừ ở ngày duyệt hoàn");
        Ve(NgayHoan).GetProperty("platformRevenue").GetDecimal().Should().Be(-15_000m,
            "phí nền tảng của phần đã hoàn (5.000 + 10.000) cũng được trừ lại");

        var tongQuan = await admin.GetAsync($"/api/v1/analytics/admin-overview?from={from}&to={to}");
        tongQuan.EnsureSuccessStatusCode();
        (await tongQuan.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data")
            .GetProperty("platformRevenueInPeriod").GetDecimal()
            .Should().Be(5_000m, "thẻ tổng quan cùng định nghĩa với biểu đồ: 20.000đ phí trừ 15.000đ phí đã hoàn");
    }
}
