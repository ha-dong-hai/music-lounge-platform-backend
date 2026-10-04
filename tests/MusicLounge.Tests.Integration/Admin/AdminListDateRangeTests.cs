using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Admin;

/// <summary>
/// MLACP-598. Danh sách tài khoản và lịch sử khiếu nại của Admin lọc được theo khoảng ngày (ngày đăng ký / ngày gửi).
/// Trước đây chỉ có tìm kiếm và trạng thái, muốn xem "tháng trước có ai đăng ký" phải lật từng trang.
/// </summary>
[Collection("Integration")]
public sealed class AdminListDateRangeTests
{
    private readonly ApiFactory _factory;

    public AdminListDateRangeTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private static string Q(DateTimeOffset d) => Uri.EscapeDataString(d.ToString("o"));

    private async Task<(HttpStatusCode Code, List<Guid> Ids)> DocAsync(string url)
    {
        var res = await Admin().GetAsync(url);
        var body = await res.Content.ReadAsStringAsync();
        if (res.StatusCode != HttpStatusCode.OK) return (res.StatusCode, []);
        using var doc = JsonDocument.Parse(body);
        return (res.StatusCode, doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToList());
    }

    /// <summary>Tài khoản có ngày đăng ký đặt tay. CreatedAt bị DbContext tự đóng dấu lúc thêm, nên đặt lại sau khi lưu.</summary>
    private async Task<Guid> TaiKhoanDangKyLucAsync(DateTime utc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User { Email = $"ngay-{Guid.NewGuid():N}@test.com", FullName = "Lọc theo ngày", Role = UserRole.Audience };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        user.CreatedAt = utc;
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> KhieuNaiGuiLucAsync(DateTimeOffset luc, ComplaintStatus trangThai = ComplaintStatus.Open)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var kn = new Complaint
        {
            ComplainantUserId = SeedHelper.AudienceId,
            TargetType = "show",
            TargetId = SeedHelper.ShowId,
            Category = ComplaintCategory.Other,
            Description = $"Khiếu nại lọc ngày {Guid.NewGuid():N}",
            ContactPhone = "0900000000",
            Status = trangThai,
            CreatedAt = luc
        };
        db.Add(kn);
        await db.SaveChangesAsync();
        return kn.Id;
    }

    // ---------- tài khoản ----------

    [Fact]
    public async Task TaiKhoan_LocTheoNgayDangKy_ChiTraTaiKhoanTrongKhoang()
    {
        var trong = await TaiKhoanDangKyLucAsync(new DateTime(2020, 5, 10, 3, 0, 0, DateTimeKind.Utc));
        var ngoai = await TaiKhoanDangKyLucAsync(new DateTime(2020, 7, 1, 3, 0, 0, DateTimeKind.Utc));
        var truocKhoang = await TaiKhoanDangKyLucAsync(new DateTime(2020, 3, 1, 3, 0, 0, DateTimeKind.Utc));
        var tu = new DateTimeOffset(2020, 5, 1, 0, 0, 0, TimeSpan.FromHours(7));
        var den = new DateTimeOffset(2020, 5, 31, 23, 59, 59, TimeSpan.FromHours(7));

        var (code, ids) = await DocAsync($"/api/v1/admin/users?pageSize=50&createdFrom={Q(tu)}&createdTo={Q(den)}");

        code.Should().Be(HttpStatusCode.OK);
        ids.Should().Contain(trong);
        ids.Should().NotContain(ngoai, "đăng ký SAU khoảng");
        ids.Should().NotContain(truocKhoang, "đăng ký TRƯỚC khoảng");
        ids.Should().NotContain(SeedHelper.AudienceId, "tài khoản mẫu được tạo hôm nay, ngoài khoảng tháng 5/2020");
    }

    [Fact]
    public async Task TaiKhoan_MocTheoGioVietNam_KhongLechBayTieng()
    {
        // 30/04/2020 18:00 UTC = 01/05/2020 01:00 giờ VN → THUỘC "từ 01/05 giờ VN".
        var dauThangGioVn = await TaiKhoanDangKyLucAsync(new DateTime(2020, 4, 30, 18, 0, 0, DateTimeKind.Utc));
        var tu = new DateTimeOffset(2020, 5, 1, 0, 0, 0, TimeSpan.FromHours(7));

        var (_, ids) = await DocAsync($"/api/v1/admin/users?pageSize=50&createdFrom={Q(tu)}&createdTo={Q(tu.AddDays(1))}");

        ids.Should().Contain(dauThangGioVn);
    }

    [Fact]
    public async Task TaiKhoan_TuSauDen_Tra422()
    {
        var tu = new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var (code, _) = await DocAsync($"/api/v1/admin/users?createdFrom={Q(tu)}&createdTo={Q(tu.AddDays(-1))}");

        code.Should().Be(HttpStatusCode.UnprocessableEntity, "trả rỗng sẽ bị đọc nhầm là kỳ đó không có ai đăng ký");
    }

    [Fact]
    public async Task TaiKhoan_KhongTruyenNgay_VanNhuCu()
    {
        // Tìm đúng email của tài khoản mẫu: các test khác tạo thêm nhiều tài khoản nên nó không chắc nằm ở trang đầu.
        var (code, ids) = await DocAsync("/api/v1/admin/users?pageSize=50&searchText=audience%40test.com");

        code.Should().Be(HttpStatusCode.OK);
        ids.Should().Contain(SeedHelper.AudienceId, "không truyền ngày thì không lọc theo ngày");
    }

    // ---------- khiếu nại ----------

    [Fact]
    public async Task KhieuNai_LocTheoNgayGui_ChiTraKhieuNaiTrongKhoang_VaKetHopDuocVoiTrangThai()
    {
        var trong = await KhieuNaiGuiLucAsync(new DateTimeOffset(2021, 3, 15, 9, 0, 0, TimeSpan.FromHours(7)));
        var trongNhungDaXong = await KhieuNaiGuiLucAsync(new DateTimeOffset(2021, 3, 16, 9, 0, 0, TimeSpan.FromHours(7)), ComplaintStatus.Resolved);
        var ngoai = await KhieuNaiGuiLucAsync(DateTimeOffset.UtcNow);
        var tu = new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.FromHours(7));
        var den = new DateTimeOffset(2021, 3, 31, 23, 59, 59, TimeSpan.FromHours(7));

        var (code, ids) = await DocAsync($"/api/v1/admin/complaints?pageSize=50&createdFrom={Q(tu)}&createdTo={Q(den)}");
        code.Should().Be(HttpStatusCode.OK);
        ids.Should().Contain(trong).And.Contain(trongNhungDaXong);
        ids.Should().NotContain(ngoai);

        var (_, chiMo) = await DocAsync($"/api/v1/admin/complaints?pageSize=50&status=Open&createdFrom={Q(tu)}&createdTo={Q(den)}");
        chiMo.Should().Contain(trong);
        chiMo.Should().NotContain(trongNhungDaXong);
    }

    [Fact]
    public async Task KhieuNai_TuSauDen_Tra422()
    {
        var tu = new DateTimeOffset(2021, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var (code, _) = await DocAsync($"/api/v1/admin/complaints?createdFrom={Q(tu)}&createdTo={Q(tu.AddDays(-1))}");

        code.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
