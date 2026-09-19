using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-463. Trang tổng quan của Admin có sẵn 4 khối biểu đồ nhưng không endpoint nào cấp dữ liệu, nên frontend đã phải
/// gỡ hẳn chúng khỏi màn hình.
///
/// Điểm dễ nói dối nhất của một trang tổng quan là gộp "tiền người mua trả" với "tiền nền tảng thực nhận" thành một chữ
/// doanh thu. Hai con số này được tách riêng, và các bài dưới đây kiểm đúng chỗ đó: một khoản thanh toán không sinh bút
/// toán nào phải làm tăng GMV mà KHÔNG làm tăng phần nền tảng nhận.
/// </summary>
[Collection("Integration")]
public sealed class AdminDashboardTests
{
    private readonly ApiFactory _factory;

    public AdminDashboardTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<JsonElement> DocAsync(string query = "")
    {
        var res = await Admin().GetAsync($"/api/v1/analytics/admin-dashboard{query}");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    private static (decimal Gmv, decimal ThucNhan) Nguon(JsonElement data, string thang, string nguon)
    {
        var cot = data.GetProperty("months").EnumerateArray()
            .Single(m => m.GetProperty("month").GetString() == thang)
            .GetProperty(nguon);
        return (cot.GetProperty("gmv").GetDecimal(), cot.GetProperty("platformRevenue").GetDecimal());
    }

    private static string ThangNay() => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM");

    /// <summary>
    /// Một khoản thanh toán đã xác nhận. <paramref name="hoaHong"/> là phần nền tảng được hưởng (cột PlatformFee).
    /// <paramref name="giuHo"/> &gt; 0 thì ghi thêm một bút toán ghi CÓ vào tài khoản nền tảng mô phỏng TIỀN GIỮ HỘ chủ
    /// phòng trà — thứ nằm ở tài khoản nền tảng nhưng KHÔNG phải doanh thu của nền tảng.
    /// </summary>
    private async Task ThanhToanAsync(string referenceType, decimal gross, decimal hoaHong = 0m, decimal giuHo = 0m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"DASH-{Guid.NewGuid():N}"[..30],
            GrossAmount = gross,
            PlatformFee = hoaHong,
            NetAmount = gross - hoaHong,
            Method = PaymentMethod.Gateway,
            Status = PaymentStatus.Confirmed,
            ReferenceType = referenceType,
            ReferenceId = "1",
            PaidAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        if (giuHo <= 0) return;

        var taiKhoanNenTang = await db.Set<Account>().FirstOrDefaultAsync(a => a.OwnerType == AccountType.Platform);
        if (taiKhoanNenTang is null)
        {
            taiKhoanNenTang = new Account { OwnerType = AccountType.Platform, OwnerId = null };
            db.Add(taiKhoanNenTang);
            await db.SaveChangesAsync();
        }

        db.Add(new LedgerEntry
        {
            JournalId = Guid.NewGuid().ToString("N"),
            AccountId = taiKhoanNenTang.Id,
            Amount = giuHo,
            IsDebit = false,
            ReferenceType = "payment",
            ReferenceId = payment.Id.ToString(),
            Description = $"Giữ hộ chủ phòng trà — chờ quyết toán",
            PaymentId = payment.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Buổi hòa nhạc đã bán <paramref name="soVe"/> vé, mỗi vé <paramref name="giaVe"/> đồng.</summary>
    private async Task<(int ShowId, string Ten)> BuoiHoaNhacCoVeAsync(decimal giaVe, int soVe, int? genreId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"dash-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"DashVenue-{Guid.NewGuid():N}"[..28], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"DashShow-{Guid.NewGuid():N}"[..28], Description = "test",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(5), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(5).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        if (genreId is { } g)
        {
            db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = g });
            await db.SaveChangesAsync();
        }

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Thường", AccessType = AccessType.Physical, TotalCapacity = 100,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Giá thường", Price = giaVe, Quota = 100, PurchaseChannel = PurchaseChannel.Both,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(4)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        for (var i = 0; i < soVe; i++)
            db.Add(new Ticket
            {
                Id = Guid.NewGuid(), PriceId = price.Id, TierId = tier.Id, ShowId = show.Id,
                Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();

        return (show.Id, show.Name);
    }

    // ---------- khối tháng ----------

    [Fact]
    public async Task LuonTraDuSauThang_KeCaThangKhongCoGiaoDich()
    {
        var data = await DocAsync();

        var thang = data.GetProperty("months").EnumerateArray()
            .Select(m => m.GetProperty("month").GetString()!).ToList();

        thang.Should().HaveCount(6, "thiếu cột sẽ bị đọc nhầm thành 'chưa có dữ liệu' thay vì 'không bán được gì'");
        thang.Should().BeInAscendingOrder();
        thang.Last().Should().Be(ThangNay(), "tháng cuối là tháng hiện tại, chưa trọn");
        thang.Should().OnlyContain(t => t.Length == 7 && t[4] == '-');
    }

    [Fact]
    public async Task TachNguon_VeGoiDonate_KhongGopLanNhau()
    {
        var thang = ThangNay();
        var truoc = await DocAsync();

        await ThanhToanAsync("TicketHold", 500_000m);
        await ThanhToanAsync("Subscription", 300_000m);
        await ThanhToanAsync("Donation", 100_000m);

        var sau = await DocAsync();

        (Nguon(sau, thang, "ticket").Gmv - Nguon(truoc, thang, "ticket").Gmv).Should().Be(500_000m);
        (Nguon(sau, thang, "package").Gmv - Nguon(truoc, thang, "package").Gmv).Should().Be(300_000m);
        (Nguon(sau, thang, "donation").Gmv - Nguon(truoc, thang, "donation").Gmv).Should().Be(100_000m);
    }

    [Fact]
    public async Task TienNguoiMuaTra_KhacTienNenTangNhan()
    {
        var thang = ThangNay();
        var truoc = await DocAsync();

        // Vé bán tại quầy: phòng trà thu tiền mặt trực tiếp, nền tảng không hưởng hoa hồng.
        await ThanhToanAsync("WalkIn", 400_000m, hoaHong: 0m);
        // Vé bán online: nền tảng hưởng 150.000đ hoa hồng, và giữ hộ 850.000đ cho chủ phòng trà tới khi quyết toán.
        // Khoản giữ hộ đó CŨNG nằm ở tài khoản nền tảng trong sổ cái — đúng chỗ phép tính cũ đếm nhầm thành doanh thu.
        await ThanhToanAsync("TicketHold", 1_000_000m, hoaHong: 150_000m, giuHo: 850_000m);

        var sau = await DocAsync();
        var (gmvTruoc, nhanTruoc) = Nguon(truoc, thang, "ticket");
        var (gmvSau, nhanSau) = Nguon(sau, thang, "ticket");

        (gmvSau - gmvTruoc).Should().Be(1_400_000m, "cả hai đều là tiền người mua trả");
        (nhanSau - nhanTruoc).Should().Be(150_000m,
            "chỉ hoa hồng mới là doanh thu nền tảng — 850.000đ giữ hộ chủ phòng trà rồi sẽ đi ra, cộng vào là báo cáo " +
            "rằng nền tảng ăn gần trọn mỗi vé");
    }

    /// <summary>
    /// MLACP-463. Hai màn hình quản trị cùng nói "doanh thu nền tảng" thì phải ra cùng một con số. Thẻ tổng quan
    /// (<c>/analytics/admin-overview</c>) trước đây cộng MỌI bút toán ghi CÓ vào tài khoản nền tảng — gồm cả tiền giữ hộ
    /// chủ phòng trà — nên nó luôn lớn hơn hẳn tổng của biểu đồ. Đúng bệnh "hai con số cùng tên, cùng trang, khác nhau".
    /// </summary>
    [Fact]
    public async Task ThePhanTramTongQuan_VaBieuDo_CungMotConSoDoanhThu()
    {
        var thang = ThangNay();
        await ThanhToanAsync("TicketHold", 1_000_000m, hoaHong: 150_000m, giuHo: 850_000m);
        await ThanhToanAsync("Subscription", 500_000m, hoaHong: 500_000m);

        var dauThang = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var tuNgay = new DateTimeOffset(dauThang.Year, dauThang.Month, 1, 0, 0, 0, TimeSpan.FromHours(7));
        var res = await Admin().GetAsync(
            $"/api/v1/analytics/admin-overview?from={Uri.EscapeDataString(tuNgay.ToString("o"))}" +
            $"&to={Uri.EscapeDataString(tuNgay.AddMonths(1).AddTicks(-1).ToString("o"))}");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var theTongQuan = doc.RootElement.GetProperty("data").GetProperty("platformRevenueInPeriod").GetDecimal();

        var data = await DocAsync();
        var tongBieuDo = Nguon(data, thang, "ticket").ThucNhan
            + Nguon(data, thang, "package").ThucNhan
            + Nguon(data, thang, "donation").ThucNhan;

        theTongQuan.Should().Be(tongBieuDo,
            "cùng một khái niệm thì phải cùng một phép tính — lệch nhau là một trong hai đang nói sai");
    }

    [Fact]
    public async Task DonGoiMon_KhongBiGopVaoDoanhThuVe()
    {
        var thang = ThangNay();
        var truoc = await DocAsync();

        await ThanhToanAsync("FnbOrder", 250_000m);

        var sau = await DocAsync();

        (Nguon(sau, thang, "ticket").Gmv - Nguon(truoc, thang, "ticket").Gmv).Should().Be(0m,
            "gộp đồ ăn uống vào vé sẽ làm doanh thu vé cao hơn sự thật");
    }

    // ---------- top buổi hòa nhạc và thể loại ----------

    [Fact]
    public async Task TopBuoiHoaNhac_XepTheoDoanhThuVe_VaDuThongTinDeHienBang()
    {
        // Cố ý dùng số tiền rất lớn: bảng xếp hạng là TOÀN NỀN TẢNG, mà các bài test khác trong cùng phiên cũng bán vé.
        // Với số tiền đời thường, hai buổi này rớt khỏi top và bài test hỏng vì lý do chẳng liên quan (đã xảy ra khi chạy
        // full suite). Số lớn khiến chúng chắc chắn đứng đầu, nên phép so thứ tự mới nói lên điều nó định nói.
        var (showCao, tenCao) = await BuoiHoaNhacCoVeAsync(giaVe: 900_000_000m, soVe: 5);
        var (showThap, _) = await BuoiHoaNhacCoVeAsync(giaVe: 300_000_000m, soVe: 2);

        var data = await DocAsync("?limit=50");
        var top = data.GetProperty("topShows").EnumerateArray().ToList();

        var dongCao = top.Single(x => x.GetProperty("showId").GetInt32() == showCao);
        dongCao.GetProperty("title").GetString().Should().Be(tenCao);
        dongCao.GetProperty("ticketsSold").GetInt32().Should().Be(5);
        dongCao.GetProperty("ticketRevenue").GetDecimal().Should().Be(4_500_000_000m);
        dongCao.GetProperty("loungeName").GetString().Should().NotBeNullOrEmpty("bảng cần tên phòng trà");
        dongCao.GetProperty("startTime").ValueKind.Should().NotBe(JsonValueKind.Null);

        var viTriCao = top.FindIndex(x => x.GetProperty("showId").GetInt32() == showCao);
        var viTriThap = top.FindIndex(x => x.GetProperty("showId").GetInt32() == showThap);
        viTriCao.Should().BeLessThan(viTriThap, "xếp theo doanh thu giảm dần");
    }

    [Fact]
    public async Task Limit_GioiHanSoDongTraVe()
    {
        await BuoiHoaNhacCoVeAsync(giaVe: 100_000m, soVe: 1);
        await BuoiHoaNhacCoVeAsync(giaVe: 200_000m, soVe: 1);

        var data = await DocAsync("?limit=1");

        data.GetProperty("topShows").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task TheLoai_DemVeTheoTungTheLoai()
    {
        var (showId, _) = await BuoiHoaNhacCoVeAsync(giaVe: 100_000m, soVe: 3, genreId: SeedHelper.GenreId1);

        var data = await DocAsync();
        var theLoai = data.GetProperty("genres").EnumerateArray()
            .FirstOrDefault(g => g.GetProperty("genreId").GetInt32() == SeedHelper.GenreId1);

        theLoai.ValueKind.Should().Be(JsonValueKind.Object, "thể loại của buổi hòa nhạc vừa bán vé phải xuất hiện");
        theLoai.GetProperty("genreName").GetString().Should().NotBeNullOrEmpty();
        theLoai.GetProperty("ticketsSold").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        theLoai.GetProperty("showCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        showId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task KhongPhaiAdmin_ThiKhongDocDuoc()
    {
        var chu = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await chu.GetAsync("/api/v1/analytics/admin-dashboard");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden, "đây là số liệu toàn nền tảng");
    }
}
