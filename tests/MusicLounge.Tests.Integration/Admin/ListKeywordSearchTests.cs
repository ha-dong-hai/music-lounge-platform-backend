using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Admin;

/// <summary>
/// MLACP-502. Bốn danh sách chỉ tìm được "trong trang đang xem" (khiếu nại, hồ sơ phòng trà, tất cả buổi diễn của Admin)
/// hoặc phải tải tới 500 bản ghi để tự tìm (danh sách phòng trà công khai). Mỗi test đặt bản ghi cần tìm ở NGOÀI trang
/// đầu, rồi tìm bằng từ khoá viết khác hoa/thường — phải tìm ra ở trang 1. Từ khoá dùng chữ không dấu vì lower() của
/// SQLite (provider test) chỉ hạ chữ ASCII — xem SearchKeyword.
/// </summary>
[Collection("Integration")]
public sealed class ListKeywordSearchTests
{
    private readonly ApiFactory _factory;

    public ListKeywordSearchTests(ApiFactory factory) => _factory = factory;

    private static string Ma() => Guid.NewGuid().ToString("N")[..10];

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private static async Task<(List<int> Ids, int Total)> GoiAsync(HttpClient client, string url, string truongId = "id")
    {
        var res = await client.GetAsync(url);
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        return (data.GetProperty("items").EnumerateArray().Select(x => x.GetProperty(truongId).GetInt32()).ToList(),
                data.GetProperty("totalCount").GetInt32());
    }

    private async Task<T> VoiDbAsync<T>(Func<ApplicationDbContext, Task<T>> viec)
    {
        using var scope = _factory.Services.CreateScope();
        return await viec(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    // Mỗi chủ chỉ có một phòng trà (ràng buộc duy nhất trên OwnerId) — mỗi phòng trà một chủ mới, trừ khi truyền chủ vào.
    private static MusicLoungeEntity PhongTra(User? chu, string ten, LoungeStatus trangThai) => new()
    {
        Owner = chu ?? new User { Email = $"o502-{Ma()}@test.com", FullName = $"Chu {Ma()}" },
        Name = ten, Status = trangThai,
        Address = new VenueAddress { Street = "1 Le Loi", District = "1", City = "HCM" }
    };

    // ---------- /admin/complaints ----------

    [Fact]
    public async Task KhieuNai_TimTheoNoiDungSdtVaMa_DuNamNgoaiTrang1()
    {
        var tu = Ma();
        var (canTim, sdt) = await VoiDbAsync(async db =>
        {
            // Sắp mới nhất trước → bản tạo ĐẦU TIÊN nằm cuối danh sách, ngoài trang 1.
            var dau = new Complaint
            {
                TargetType = "show", TargetId = SeedHelper.ShowId, Category = ComplaintCategory.Other,
                Description = $"Am thanh ro rit {tu} suot buoi", ContactPhone = $"09{Random.Shared.Next(10_000_000, 99_999_999)}",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(dau);
            await db.SaveChangesAsync();
            db.AddRange(Enumerable.Range(0, 25).Select(_ => new Complaint
            {
                TargetType = "show", TargetId = SeedHelper.ShowId, Category = ComplaintCategory.Other,
                Description = $"Khieu nai khac {Ma()}", CreatedAt = DateTimeOffset.UtcNow
            }));
            await db.SaveChangesAsync();
            return (dau.Id, dau.ContactPhone!);
        });

        (await GoiAsync(Admin(), $"/api/v1/admin/complaints?keyword={tu.ToUpperInvariant()}&pageSize=10")).Ids
            .Should().Equal([canTim], "tìm theo nội dung, không phân biệt hoa thường");
        (await GoiAsync(Admin(), $"/api/v1/admin/complaints?keyword={sdt}&pageSize=10")).Ids
            .Should().Equal([canTim], "tìm theo SĐT liên hệ");
        (await GoiAsync(Admin(), $"/api/v1/admin/complaints?keyword={canTim}&pageSize=50")).Ids
            .Should().Contain(canTim, "từ khoá là số thì khớp đúng mã khiếu nại");
    }

    // ---------- /admin/venues/pending ----------

    [Fact]
    public async Task HoSoPhongTra_TimTheoTenChu_DuNamNgoaiTrang1()
    {
        var tu = Ma();
        var canTim = await VoiDbAsync(async db =>
        {
            var chu = new User { Email = $"v502-{Ma()}@test.com", FullName = $"Nguyen Van {tu}" };
            // Sắp cũ nhất trước → bản tạo SAU CÙNG nằm ngoài trang 1.
            db.AddRange(Enumerable.Range(0, 25).Select(_ => PhongTra(null, $"Cho duyet {Ma()}", LoungeStatus.Pending)));
            await db.SaveChangesAsync();
            var l = PhongTra(chu, $"Phong {Ma()}", LoungeStatus.Pending);
            db.Add(l);
            await db.SaveChangesAsync();
            return l.Id;
        });

        var (ids, total) = await GoiAsync(Admin(), $"/api/v1/admin/venues/pending?keyword={tu.ToUpperInvariant()}&pageSize=10", "loungeId");

        ids.Should().Equal([canTim]);
        total.Should().Be(1);
    }

    [Fact]
    public async Task HoSoPhongTra_AllStatuses_LietKeMoiTrangThai_MacDinhVanChiChoDuyet()
    {
        var tu = Ma();
        var (choDuyet, daDuyet) = await VoiDbAsync(async db =>
        {
            var a = PhongTra(null, $"Tat ca {tu} A", LoungeStatus.Pending);
            var b = PhongTra(null, $"Tat ca {tu} B", LoungeStatus.Approved);
            db.AddRange(a, b);
            await db.SaveChangesAsync();
            return (a.Id, b.Id);
        });

        (await GoiAsync(Admin(), $"/api/v1/admin/venues/pending?keyword={tu}&allStatuses=true", "loungeId")).Ids
            .Should().BeEquivalentTo([choDuyet, daDuyet], "allStatuses bỏ qua lọc trạng thái (M-420)");
        (await GoiAsync(Admin(), $"/api/v1/admin/venues/pending?keyword={tu}", "loungeId")).Ids
            .Should().Equal([choDuyet], "không truyền allStatuses thì vẫn là hàng chờ duyệt như cũ");
    }

    // ---------- GET /lounge-shows (Tất cả buổi diễn của Admin) ----------

    [Fact]
    public async Task BuoiDienCongKhai_TimTheoTenBuoiVaTenPhongTra()
    {
        var tuBuoi = Ma();
        var tuPhong = Ma();
        var (theoTenBuoi, theoTenPhong) = await VoiDbAsync(async db =>
        {
            var phong = PhongTra(null, $"Phong tra {tuPhong}", LoungeStatus.Approved);
            db.Add(phong);
            await db.SaveChangesAsync();
            LoungeShow Buoi(int loungeId, string ten) => new()
            {
                LoungeId = loungeId, Name = ten, Description = "test", Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(9), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(9).AddHours(2)
            };
            var a = Buoi(SeedHelper.LoungeId, $"Dem nhac {tuBuoi}");
            var b = Buoi(phong.Id, $"Dem khac {Ma()}");
            db.AddRange(a, b);
            db.AddRange(Enumerable.Range(0, 25).Select(_ => Buoi(SeedHelper.LoungeId, $"Lap day {Ma()}")));
            await db.SaveChangesAsync();
            return (a.Id, b.Id);
        });

        (await GoiAsync(_factory.CreateClient(), $"/api/v1/lounge-shows?keyword={tuBuoi.ToUpperInvariant()}&pageSize=10")).Ids
            .Should().Equal([theoTenBuoi]);
        (await GoiAsync(_factory.CreateClient(), $"/api/v1/lounge-shows?keyword={tuPhong}&pageSize=10")).Ids
            .Should().Equal([theoTenPhong], "gõ tên phòng trà ra các buổi của phòng trà đó");
    }

    // Gộp với MLACP-498: đường mine=true không có keyword — gửi kèm thì 400 chứ không lặng lẽ bỏ qua.
    [Fact]
    public async Task BuoiCuaToi_GuiKemKeyword_400()
    {
        var chu = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await chu.GetAsync("/api/v1/lounge-shows?mine=true&keyword=abc");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, await res.Content.ReadAsStringAsync());
    }

    // ---------- GET /lounges ----------

    [Fact]
    public async Task PhongTraCongKhai_TimTheoTen_KhongCanTaiHetDanhSach()
    {
        var tu = Ma();
        var canTim = await VoiDbAsync(async db =>
        {
            db.AddRange(Enumerable.Range(0, 55).Select(_ => PhongTra(null, $"A {Ma()}", LoungeStatus.Approved)));
            // Sắp theo tên → "ZZ..." nằm cuối, ngoài trang 1 (trang tối đa 50).
            var l = PhongTra(null, $"ZZ Phong {tu}", LoungeStatus.Approved);
            db.Add(l);
            await db.SaveChangesAsync();
            return l.Id;
        });

        var (ids, total) = await GoiAsync(_factory.CreateClient(), $"/api/v1/lounges?keyword={tu.ToUpperInvariant()}&pageSize=10");

        ids.Should().Equal([canTim]);
        total.Should().Be(1);
    }
}
