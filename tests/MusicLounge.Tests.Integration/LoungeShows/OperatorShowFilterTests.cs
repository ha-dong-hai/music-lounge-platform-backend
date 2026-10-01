using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-498. Ô chọn buổi ở Vận hành đêm diễn và Phát trực tuyến tải 100 buổi qua <c>?mine=true</c> rồi lọc
/// Published/Online ở trình duyệt — phòng trà có trên 100 buổi thì buổi cần chọn có thể không bao giờ hiện ra.
/// Bộ lọc phải chạy ở máy chủ, trước phân trang, và giống nhau cho chủ phòng trà lẫn nhân viên.
/// Mỗi test dùng phòng trà riêng: phòng trà seed bị các test khác thêm buổi vào, đếm totalCount trên đó không chính xác.
/// </summary>
[Collection("Integration")]
public sealed class OperatorShowFilterTests
{
    private readonly ApiFactory _factory;

    public OperatorShowFilterTests(ApiFactory factory) => _factory = factory;

    private async Task<(int LoungeId, int OwnerId)> PhongTraRiengAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"l498-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra 498" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeEntity
        {
            OwnerId = owner.Id, Name = $"L498 {Guid.NewGuid():N}"[..14], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, owner.Id);
    }

    // Nhân viên seed đang làm ở phòng trà seed (mỗi tài khoản một phòng trà) — tạo nhân viên riêng cho phòng trà riêng,
    // có bản ghi LoungeStaff thật để qua được kiểm "còn là nhân viên" của pipeline.
    private async Task<int> NhanVienRiengAsync(int loungeId, int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staff = new User { Email = $"s498-{Guid.NewGuid():N}@test.com", FullName = "Nhan vien 498" };
        db.Users.Add(staff);
        await db.SaveChangesAsync();
        db.Add(new LoungeStaff
        {
            LoungeId = loungeId, UserId = staff.Id, AssignedBy = ownerId, IsActive = true, AssignedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return staff.Id;
    }

    private async Task<List<int>> ThemBuoiAsync(int loungeId, LoungeShowStatus trangThai, LoungeShowFormat hinhThuc, int soLuong = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var shows = Enumerable.Range(0, soLuong).Select(i => new LoungeShow
        {
            LoungeId = loungeId, Name = $"L498-{Guid.NewGuid():N}", Description = "test",
            Format = hinhThuc, Status = trangThai,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3 + i), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3 + i).AddHours(2)
        }).ToList();
        db.LoungeShows.AddRange(shows);
        await db.SaveChangesAsync();
        return shows.Select(s => s.Id).ToList();
    }

    private static async Task<(List<int> Ids, int Total)> GoiAsync(HttpClient client, string query)
    {
        var res = await client.GetAsync($"/api/v1/lounge-shows?{query}");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        return (data.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList(),
                data.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task NhanVien_LocTrangThaiVaHinhThuc_ChiNhanBuoiKhopCuaPhongTraMinh()
    {
        var (loungeId, ownerId) = await PhongTraRiengAsync();
        var khop = await ThemBuoiAsync(loungeId, LoungeShowStatus.Published, LoungeShowFormat.Online, 2);
        await ThemBuoiAsync(loungeId, LoungeShowStatus.Published, LoungeShowFormat.Offline);
        await ThemBuoiAsync(loungeId, LoungeShowStatus.Published, LoungeShowFormat.Hybrid);
        await ThemBuoiAsync(loungeId, LoungeShowStatus.Draft, LoungeShowFormat.Online);
        var phongTraKhac = await ThemBuoiAsync(SeedHelper.LoungeId, LoungeShowStatus.Published, LoungeShowFormat.Online);
        var nhanVien = _factory.CreateAuthenticatedClient(await NhanVienRiengAsync(loungeId, ownerId), "Staff", loungeId);

        var (ids, total) = await GoiAsync(nhanVien, "mine=true&status=Published&format=Online&pageSize=100");

        ids.Should().BeEquivalentTo(khop, "chỉ buổi Published + Online của đúng phòng trà nhân viên vận hành");
        ids.Should().NotContain(phongTraKhac);
        total.Should().Be(2, "totalCount phải đếm theo bộ lọc, không phải theo cả phòng trà");
    }

    [Fact]
    public async Task ChuPhongTra_120Buoi_LocRoiPhanTrang_Trang2TraDungPhanConLai()
    {
        var (loungeId, ownerId) = await PhongTraRiengAsync();
        var khop = await ThemBuoiAsync(loungeId, LoungeShowStatus.Published, LoungeShowFormat.Online, 70);
        await ThemBuoiAsync(loungeId, LoungeShowStatus.Published, LoungeShowFormat.Offline, 30);
        await ThemBuoiAsync(loungeId, LoungeShowStatus.Draft, LoungeShowFormat.Online, 20);
        var chu = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);

        var (trang1, total1) = await GoiAsync(chu, "mine=true&status=Published&format=Online&page=1&pageSize=50");
        var (trang2, total2) = await GoiAsync(chu, "mine=true&status=Published&format=Online&page=2&pageSize=50");

        total1.Should().Be(70);
        total2.Should().Be(70);
        trang1.Should().HaveCount(50);
        trang2.Should().HaveCount(20, "lọc trước khi cắt trang — trang 2 là 20 buổi khớp còn lại, không lẫn buổi khác");
        trang1.Concat(trang2).Should().BeEquivalentTo(khop, "hai trang ghép lại đúng bằng tập khớp, không trùng không sót");
    }

    [Fact]
    public async Task KhongTruyenThamSoMoi_TraNhuCu_DuMoiTrangThaiVaHinhThuc()
    {
        var (loungeId, ownerId) = await PhongTraRiengAsync();
        var tatCa = new List<int>();
        tatCa.AddRange(await ThemBuoiAsync(loungeId, LoungeShowStatus.Draft, LoungeShowFormat.Offline));
        tatCa.AddRange(await ThemBuoiAsync(loungeId, LoungeShowStatus.Published, LoungeShowFormat.Hybrid));
        tatCa.AddRange(await ThemBuoiAsync(loungeId, LoungeShowStatus.Cancelled, LoungeShowFormat.Online));
        var chu = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);

        var (ids, total) = await GoiAsync(chu, "mine=true&pageSize=100");

        ids.Should().BeEquivalentTo(tatCa);
        total.Should().Be(3);
    }

    [Theory]
    [InlineData("status=Published")]
    [InlineData("format=Online")]
    public async Task DanhSachCongKhai_GuiBoLocChiDanhChoMine_Tra400ChuKhongLangLeBoQua(string boLoc)
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/lounge-shows?{boLoc}");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, await res.Content.ReadAsStringAsync());
    }
}
