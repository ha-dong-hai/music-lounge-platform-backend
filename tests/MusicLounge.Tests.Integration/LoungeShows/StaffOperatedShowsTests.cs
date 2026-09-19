using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-466. Nhân viên phòng trà vào trang livestream thì thấy danh sách TRỐNG: "chưa có buổi diễn Online" dù phòng trà
/// có. Danh sách "buổi hòa nhạc của tôi" lọc theo người sở hữu phòng trà, mà nhân viên không sở hữu buổi nào — trong khi
/// các thao tác livestream lại cho phép nhân viên. Có quyền thao tác mà không biết thao tác trên buổi nào.
/// </summary>
[Collection("Integration")]
public sealed class StaffOperatedShowsTests
{
    private readonly ApiFactory _factory;

    public StaffOperatedShowsTests(ApiFactory factory) => _factory = factory;

    private async Task<int> BuoiHoaNhacAsync(int loungeId, LoungeShowStatus trangThai)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = loungeId, Name = $"NhanVien-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Online, Status = trangThai,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(2)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private static async Task<IReadOnlyList<int>> DanhSachCuaToiAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/v1/lounge-shows?mine=true&page=1&pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetInt32()).ToList();
    }

    [Fact]
    public async Task NhanVien_ThayCacBuoiHoaNhacCuaPhongTraMinhVanHanh()
    {
        var daCongKhai = await BuoiHoaNhacAsync(SeedHelper.LoungeId, LoungeShowStatus.Published);
        var banNhap = await BuoiHoaNhacAsync(SeedHelper.LoungeId, LoungeShowStatus.Draft);
        var nhanVien = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var ds = await DanhSachCuaToiAsync(nhanVien);

        ds.Should().Contain(daCongKhai, "nhân viên phải thấy buổi của phòng trà mình để còn điều khiển livestream");
        ds.Should().Contain(banNhap, "người vận hành cần thấy đủ mọi trạng thái, không chỉ những buổi đã công khai");
    }

    [Fact]
    public async Task NhanVienPhongTraKhac_KhongThayBuoiCuaPhongTraNay()
    {
        var show = await BuoiHoaNhacAsync(SeedHelper.LoungeId, LoungeShowStatus.Published);
        var nhanVienNoiKhac = _factory.CreateAuthenticatedClient(
            SeedHelper.OtherVenueStaffId, "Staff", SeedHelper.OtherLoungeId);

        var ds = await DanhSachCuaToiAsync(nhanVienNoiKhac);

        ds.Should().NotContain(show, "mỗi nhân viên chỉ vận hành đúng phòng trà ghi trong token của mình");
    }

    [Fact]
    public async Task NhanVienChuaDuocPhanCong_NhanDanhSachRong_KhongPhaiLoi()
    {
        await BuoiHoaNhacAsync(SeedHelper.LoungeId, LoungeShowStatus.Published);
        var chuaPhanCong = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff");

        var ds = await DanhSachCuaToiAsync(chuaPhanCong);

        ds.Should().BeEmpty("không được phân công phòng trà nào thì không có buổi nào để vận hành");
    }

    [Fact]
    public async Task ChuPhongTra_VanThayBuoiCuaMinhNhuTruoc()
    {
        var show = await BuoiHoaNhacAsync(SeedHelper.LoungeId, LoungeShowStatus.Draft);
        var chu = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var ds = await DanhSachCuaToiAsync(chu);

        ds.Should().Contain(show, "sửa cho nhân viên không được làm hỏng đường của chủ phòng trà");
    }
}
