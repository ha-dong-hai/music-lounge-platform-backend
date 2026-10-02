using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// MLACP-521. Từ 01/7/2025 cả nước còn 34 tỉnh, bỏ cấp huyện (NQ 202/2025/QH15); danh mục và mã chính thức theo
/// QĐ 19/2025/QĐ-TTg. Địa chỉ phòng trà nhận MÃ tỉnh/xã (tên lấy từ danh mục) nhưng VẪN nhận chữ gõ tay như cũ — giao
/// diện chưa cập nhật không được hỏng. Mỗi bài tạo chủ phòng trà riêng (một chủ chỉ có một phòng trà).
/// </summary>
[Collection("Integration")]
public sealed class AdministrativeUnitAddressTests
{
    private const string HoChiMinh = "79";
    private const string PhuongSaiGon = "26740";
    private const string PhuongHoanKiem = "00070"; // thuộc Hà Nội (01), không thuộc TP.HCM

    private readonly ApiFactory _factory;

    public AdministrativeUnitAddressTests(ApiFactory factory) => _factory = factory;

    private async Task<Guid> FreshOwnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"m521-{Guid.NewGuid():N}@test.com", FullName = "Lê Thu Hà", Role = UserRole.Owner };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        return owner.Id;
    }

    private static object TaoPhongTra(string? provinceCode, string? wardCode, string? city = null, string? ward = null) => new
    {
        Name = $"Phòng trà {Guid.NewGuid():N}"[..20],
        Description = (string?)null,
        AtmosphereId = (int?)null,
        Street = "36 Nguyễn Thị Nghĩa",
        Ward = ward,
        City = city,
        Latitude = (double?)null,
        Longitude = (double?)null,
        ProvinceCode = provinceCode,
        WardCode = wardCode
    };

    private static async Task<JsonElement> DataAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    // ─── Danh mục ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DanhMuc_Du34TinhVa3321Xa_TheoQd19()
    {
        var client = _factory.CreateClient();
        var tinh = await DataAsync(await client.GetAsync("/api/v1/catalog/provinces"));
        tinh.GetArrayLength().Should().Be(34);

        var tongXa = 0;
        foreach (var p in tinh.EnumerateArray())
        {
            var xa = await DataAsync(await client.GetAsync($"/api/v1/catalog/provinces/{p.GetProperty("code").GetString()}/wards"));
            tongXa += xa.GetArrayLength();
        }
        tongXa.Should().Be(3321, "QĐ 19/2025/QĐ-TTg ban hành 3.321 đơn vị hành chính cấp xã");
    }

    [Fact]
    public async Task DanhMuc_PhuongCuaTpHcm_CoPhuongSaiGon_MaChinhThuc()
    {
        var xa = await DataAsync(await _factory.CreateClient().GetAsync($"/api/v1/catalog/provinces/{HoChiMinh}/wards"));

        xa.EnumerateArray().Should().Contain(w =>
            w.GetProperty("code").GetString() == PhuongSaiGon && w.GetProperty("name").GetString() == "Phường Sài Gòn");
    }

    [Fact]
    public async Task DanhMuc_MaTinhKhongCo_Tra404()
        => (await _factory.CreateClient().GetAsync("/api/v1/catalog/provinces/99/wards"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

    // ─── Tạo / sửa phòng trà ──────────────────────────────────────────────────

    [Fact]
    public async Task TaoTheoMa_TenLayTuDanhMuc_KhongCoQuan()
    {
        var owner = _factory.CreateAuthenticatedClient(await FreshOwnerAsync(), "Owner");

        // Chữ client gửi kèm KHÔNG được tin: tên phải lấy từ danh mục theo mã.
        var res = await owner.PostAsJsonAsync("/api/v1/lounges", TaoPhongTra(HoChiMinh, PhuongSaiGon, city: "HCM", ward: "Bến Nghé"));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await DataAsync(res)).GetGuid();

        var chiTiet = await DataAsync(await owner.GetAsync($"/api/v1/lounges/{id}"));
        chiTiet.GetProperty("provinceCode").GetString().Should().Be(HoChiMinh);
        chiTiet.GetProperty("wardCode").GetString().Should().Be(PhuongSaiGon);
        chiTiet.GetProperty("city").GetString().Should().Be("Thành phố Hồ Chí Minh");
        chiTiet.GetProperty("ward").GetString().Should().Be("Phường Sài Gòn");
        chiTiet.GetProperty("district").GetString().Should().BeEmpty("không còn cấp huyện từ 01/7/2025");
    }

    [Fact]
    public async Task TaoKieuCu_ChiGoChu_VanDuocNhan()
    {
        var owner = _factory.CreateAuthenticatedClient(await FreshOwnerAsync(), "Owner");

        var res = await owner.PostAsJsonAsync("/api/v1/lounges", TaoPhongTra(null, null, city: "TP. Hồ Chí Minh", ward: "Phường Bến Thành"));

        res.StatusCode.Should().Be(HttpStatusCode.Created, "giao diện chưa cập nhật vẫn gửi chữ như trước");
    }

    [Theory]
    [InlineData(HoChiMinh, PhuongHoanKiem, "không thuộc")]     // phường của Hà Nội gắn vào TP.HCM
    [InlineData(null, PhuongSaiGon, "trước khi chọn phường")]  // có phường mà không có tỉnh
    [InlineData("99", null, "không có trong danh mục")]        // tỉnh không tồn tại
    [InlineData(HoChiMinh, "99999", "không có trong danh mục")] // phường không tồn tại
    public async Task MaSai_Tra400_CoLyDo(string? province, string? ward, string lyDo)
    {
        var owner = _factory.CreateAuthenticatedClient(await FreshOwnerAsync(), "Owner");

        var res = await owner.PostAsJsonAsync("/api/v1/lounges", TaoPhongTra(province, ward, city: "TP. Hồ Chí Minh"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain(lyDo);
    }

    [Fact]
    public async Task ThieuThanhPho_MaCoMaTinh_VanHopLe_ConKhongCoCaHai_Tra400()
    {
        var owner = _factory.CreateAuthenticatedClient(await FreshOwnerAsync(), "Owner");
        (await owner.PostAsJsonAsync("/api/v1/lounges", TaoPhongTra(null, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "không có tỉnh theo mã thì phải có tên thành phố");

        (await owner.PostAsJsonAsync("/api/v1/lounges", TaoPhongTra(HoChiMinh, null)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SuaTheoMa_CapNhatMa_VaXoaQuanCu()
    {
        var ownerId = await FreshOwnerAsync();
        Guid id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lounge = new MusicLoungeEntity
            {
                OwnerId = ownerId, Name = $"Phòng trà {Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
                Address = new VenueAddress { Street = "142 Trần Quang Khải", Ward = "Đa Kao", District = "Quận 1", City = "TP.HCM" }
            };
            db.Add(lounge);
            await db.SaveChangesAsync();
            id = lounge.Id;
        }
        var owner = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await owner.PutAsJsonAsync($"/api/v1/lounges/{id}", new
        {
            Name = "Phòng trà Đa Kao", Description = (string?)null, AtmosphereId = (int?)null,
            Street = "142 Trần Quang Khải", Ward = (string?)null, District = (string?)null, City = (string?)null,
            Latitude = (double?)null, Longitude = (double?)null, ProvinceCode = HoChiMinh, WardCode = PhuongSaiGon
        });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var chiTiet = await DataAsync(await owner.GetAsync($"/api/v1/lounges/{id}"));
        chiTiet.GetProperty("wardCode").GetString().Should().Be(PhuongSaiGon);
        chiTiet.GetProperty("district").GetString().Should().BeEmpty();
        chiTiet.GetProperty("fullAddress").GetString().Should().NotContain("Quận 1");
    }

    // ─── Lọc ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DanhSachPhongTra_LocTheoMaTinh()
    {
        var ownerId = await FreshOwnerAsync();
        var ten = $"Lọc mã {Guid.NewGuid():N}"[..20];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new MusicLoungeEntity
            {
                OwnerId = ownerId, Name = ten, Status = LoungeStatus.Approved,
                Address = new VenueAddress { Street = "1 Lê Lợi", Ward = "Phường Sài Gòn", City = "Thành phố Hồ Chí Minh",
                    ProvinceCode = HoChiMinh, WardCode = PhuongSaiGon }
            });
            await db.SaveChangesAsync();
        }
        var client = _factory.CreateClient();

        async Task<bool> CoTrongDanhSach(string ma)
        {
            var data = await DataAsync(await client.GetAsync($"/api/v1/lounges?provinceCode={ma}&pageSize=100&keyword={Uri.EscapeDataString(ten)}"));
            return data.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("name").GetString() == ten);
        }

        (await CoTrongDanhSach(HoChiMinh)).Should().BeTrue();
        (await CoTrongDanhSach("01")).Should().BeFalse("phòng trà ở TP.HCM không được hiện khi lọc Hà Nội");
    }

    [Fact]
    public async Task TimBuoiDien_LocTheoMaTinh_KhongBiBoQua()
    {
        var client = _factory.CreateClient();
        async Task<int> Tong(string q)
            => (await DataAsync(await client.GetAsync($"/api/v1/lounge-shows/search?pageSize=50{q}"))).GetProperty("totalCount").GetInt32();

        var khongLoc = await Tong("");
        khongLoc.Should().BeGreaterThan(0, "phép kiểm phải có dữ liệu, nếu không nó xanh vì không có gì để lọc");
        (await Tong("&provinceCode=96")).Should().Be(0, "không phòng trà seed nào gắn mã Cà Mau — tham số phải được áp, không bị bỏ qua");
    }
}
