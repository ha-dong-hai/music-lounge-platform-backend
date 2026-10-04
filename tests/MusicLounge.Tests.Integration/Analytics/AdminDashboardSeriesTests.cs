using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-594. Biểu đồ tiền của trang tổng quan Admin trước đây luôn là 6 tháng gần nhất, bất kể Admin chọn kỳ nào.
/// Nay có thêm chuỗi <c>series</c> theo ĐÚNG khoảng from/to, tự đổi đơn vị theo độ dài khoảng (ngày / tuần / tháng).
/// </summary>
[Collection("Integration")]
public sealed class AdminDashboardSeriesTests
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private readonly ApiFactory _factory;

    public AdminDashboardSeriesTests(ApiFactory factory) => _factory = factory;

    private static DateTimeOffset HomNayVn()
    {
        var bay = DateTimeOffset.UtcNow.ToOffset(Vn);
        return new DateTimeOffset(bay.Year, bay.Month, bay.Day, 0, 0, 0, Vn);
    }

    private async Task<JsonElement> DocAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var url = $"/api/v1/analytics/admin-dashboard?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}";
        var res = await client.GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    private static List<DateTimeOffset> DauNhom(JsonElement data) =>
        data.GetProperty("series").EnumerateArray().Select(b => b.GetProperty("start").GetDateTimeOffset()).ToList();

    private static decimal TongVe(JsonElement data) =>
        data.GetProperty("series").EnumerateArray().Sum(b => b.GetProperty("ticket").GetProperty("gmv").GetDecimal());

    private async Task ThanhToanVeAsync(decimal gross, DateTimeOffset paidAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(new Payment
        {
            OrderId = $"SER-{Guid.NewGuid():N}"[..30],
            GrossAmount = gross,
            PlatformFee = 0m,
            NetAmount = gross,
            Method = PaymentMethod.Gateway,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "1",
            PaidAt = paidAt,
            CreatedAt = paidAt
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Khoang10Ngay_GopTheoNgay_DuMoiNgayKeCaNgayKhongBan()
    {
        var den = HomNayVn().AddDays(1).AddTicks(-1);
        var data = await DocAsync(HomNayVn().AddDays(-10), den);

        data.GetProperty("seriesUnit").GetString().Should().Be("day");
        var dau = DauNhom(data);
        dau.Should().HaveCount(11, "10 ngày trước tới hết hôm nay là 11 ngày, kể cả ngày không bán được gì");
        dau.Should().BeInAscendingOrder();
        dau.Zip(dau.Skip(1)).Should().OnlyContain(c => c.Second - c.First == TimeSpan.FromDays(1));
    }

    [Fact]
    public async Task Khoang90Ngay_GopTheoTuan_MoiNhomBatDauThuHai()
    {
        var data = await DocAsync(HomNayVn().AddDays(-90), HomNayVn());

        data.GetProperty("seriesUnit").GetString().Should().Be("week");
        var dau = DauNhom(data);
        dau.Should().HaveCountGreaterThanOrEqualTo(13).And.HaveCountLessThanOrEqualTo(15);
        dau.Should().OnlyContain(d => d.ToOffset(Vn).DayOfWeek == DayOfWeek.Monday && d.ToOffset(Vn).Hour == 0);
    }

    [Fact]
    public async Task KhoangMotNam_GopTheoThang()
    {
        var data = await DocAsync(HomNayVn().AddYears(-1), HomNayVn());

        data.GetProperty("seriesUnit").GetString().Should().Be("month");
        var dau = DauNhom(data);
        dau.Should().HaveCountGreaterThanOrEqualTo(12).And.HaveCountLessThanOrEqualTo(13);
        dau.Should().OnlyContain(d => d.ToOffset(Vn).Day == 1);
    }

    [Fact]
    public async Task ThanhToanTrongKy_DuocCong_ThanhToanNgoaiKy_KhongDuocCong()
    {
        var tu = HomNayVn().AddDays(-6);
        var den = HomNayVn().AddDays(1).AddTicks(-1);
        var truoc = await DocAsync(tu, den);

        await ThanhToanVeAsync(250_000m, DateTimeOffset.UtcNow);            // trong kỳ
        await ThanhToanVeAsync(999_000m, HomNayVn().AddDays(-40));          // ngoài kỳ

        var sau = await DocAsync(tu, den);

        (TongVe(sau) - TongVe(truoc)).Should().Be(250_000m, "biểu đồ phải theo đúng kỳ đã chọn, không theo 6 tháng cố định");
    }

    /// <summary>Kỳ bắt đầu GIỮA tuần: nhóm đầu tiên vẫn mang mốc thứ Hai, nhưng khoản trả trước mốc bắt đầu kỳ (cùng tuần)
    /// không được tính — nếu không, chọn "từ thứ Năm" vẫn cộng tiền của thứ Hai–thứ Tư.</summary>
    [Fact]
    public async Task KyBatDauGiuaTuan_KhoanTruocMocBatDau_KhongDuocCong()
    {
        var tu = HomNayVn().AddDays(-60);
        if (tu.DayOfWeek == DayOfWeek.Monday) tu = tu.AddDays(-1);
        var den = HomNayVn();
        var truoc = await DocAsync(tu, den);

        await ThanhToanVeAsync(777_000m, tu.AddHours(-1)); // cùng tuần với mốc bắt đầu, nhưng trước mốc

        var sau = await DocAsync(tu, den);

        sau.GetProperty("seriesUnit").GetString().Should().Be("week");
        (TongVe(sau) - TongVe(truoc)).Should().Be(0m);
    }

    [Fact]
    public async Task KhoiSauThangCu_VanGiuNguyen_DeGiaoDienCuKhongVo()
    {
        var data = await DocAsync(HomNayVn().AddDays(-10), HomNayVn());

        data.GetProperty("months").GetArrayLength().Should().Be(6);
    }
}
