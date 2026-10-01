using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Follows;

/// <summary>
/// MLACP-503. Trái tim "Theo dõi" được tính bằng cách tải trang đầu danh sách đang theo dõi (kẹp 50) rồi tìm trong đó —
/// người theo dõi 60 phòng thì phòng thứ 51–60 luôn hiện "chưa theo dõi". Hỏi thẳng theo id phải trả đúng.
/// </summary>
[Collection("Integration")]
public sealed class LoungeFollowStatusTests
{
    private readonly ApiFactory _factory;

    public LoungeFollowStatusTests(ApiFactory factory) => _factory = factory;

    private static string Ma() => Guid.NewGuid().ToString("N")[..10];

    // Người dùng mới theo dõi 60 phòng trà (theo thứ tự tạo) + 1 phòng trà không theo dõi.
    private async Task<(int UserId, List<int> DangTheoDoi, int KhongTheoDoi)> NguoiTheoDoi60PhongAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nguoi = new User { Email = $"f503-{Ma()}@test.com", FullName = "Nguoi theo doi 503" };
        db.Users.Add(nguoi);
        // Mỗi chủ chỉ một phòng trà (ràng buộc duy nhất OwnerId) — mỗi phòng một chủ mới.
        var phong = Enumerable.Range(0, 61).Select(_ => new MusicLoungeEntity
        {
            Owner = new User { Email = $"o503-{Ma()}@test.com", FullName = $"Chu {Ma()}" },
            Name = $"Phong {Ma()}", Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Le Loi", District = "1", City = "HCM" }
        }).ToList();
        db.AddRange(phong);
        await db.SaveChangesAsync();
        db.AddRange(phong.Take(60).Select(l => new Follow { UserId = nguoi.Id, LoungeId = l.Id, CreatedAt = DateTimeOffset.UtcNow }));
        await db.SaveChangesAsync();
        return (nguoi.Id, phong.Take(60).Select(l => l.Id).ToList(), phong[60].Id);
    }

    private static async Task<Dictionary<int, bool>> HoiAsync(HttpClient client, IEnumerable<int> ids)
    {
        var res = await client.GetAsync("/api/v1/follows/lounges/status?" + string.Join("&", ids.Select(i => $"loungeIds={i}")));
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .ToDictionary(x => x.GetProperty("loungeId").GetInt32(), x => x.GetProperty("isFollowing").GetBoolean());
    }

    [Fact]
    public async Task PhongThu60_TraDangTheoDoi_DuVuotTrangDauCuaDanhSachTheoDoi()
    {
        var (userId, dangTheoDoi, _) = await NguoiTheoDoi60PhongAsync();
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");

        var kq = await HoiAsync(client, [dangTheoDoi[59]]);

        kq.Should().Equal(new Dictionary<int, bool> { [dangTheoDoi[59]] = true });
    }

    [Fact]
    public async Task HoiCaTrang_MotLoiGoi_TraDungTungPhong()
    {
        var (userId, dangTheoDoi, khongTheoDoi) = await NguoiTheoDoi60PhongAsync();
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");

        var kq = await HoiAsync(client, dangTheoDoi.Append(khongTheoDoi).Append(999_999));

        kq.Should().HaveCount(62);
        kq.Where(p => dangTheoDoi.Contains(p.Key)).Should().OnlyContain(p => p.Value);
        kq[khongTheoDoi].Should().BeFalse();
        kq[999_999].Should().BeFalse("phòng trà không tồn tại: false, không 404 — không để dò id");
    }

    [Fact]
    public async Task ChiTietPhongTra_IsFollowing_CungDungVoiPhongThu60()
    {
        // Đường đã có sẵn (GetLoungeDetail) nhưng chưa test nào khoá — khoá luôn ở đây.
        var (userId, dangTheoDoi, _) = await NguoiTheoDoi60PhongAsync();
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");

        var res = await client.GetAsync($"/api/v1/lounges/{dangTheoDoi[59]}");
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

        doc.RootElement.GetProperty("data").GetProperty("isFollowing").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ChuaDangNhap_401_VaHoiQua100Phong_400()
    {
        (await _factory.CreateClient().GetAsync("/api/v1/follows/lounges/status?loungeIds=1"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        (await client.GetAsync("/api/v1/follows/lounges/status?" + string.Join("&", Enumerable.Range(1, 101).Select(i => $"loungeIds={i}"))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/v1/follows/lounges/status"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
