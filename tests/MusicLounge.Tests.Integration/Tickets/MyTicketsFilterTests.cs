using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// MLACP-499. Trang Vé của tôi lọc sắp/đã diễn, hình thức và từ khoá TRONG một trang 10 vé — tab "Đã diễn ra" ở trang 1
/// có thể trống dù trang 2 có vé. Bộ lọc phải chạy ở máy chủ trước khi cắt trang, totalCount đếm theo bộ lọc.
/// Mỗi test dùng một người mua mới để đếm chính xác (người mua seed có vé do test khác tạo).
/// </summary>
[Collection("Integration")]
public sealed class MyTicketsFilterTests
{
    private readonly ApiFactory _factory;

    public MyTicketsFilterTests(ApiFactory factory) => _factory = factory;

    private sealed record BoVe(Guid BuyerId, List<Guid> SapPhysical, List<Guid> SapLivestream,
        List<Guid> DaPhysical, List<Guid> DaLivestream, string TenBuoiDaDien)
    {
        public List<Guid> DaDien => DaPhysical.Concat(DaLivestream).ToList();
        public List<Guid> TatCa => SapPhysical.Concat(SapLivestream).Concat(DaDien).ToList();
    }

    // 25 vé: buổi sắp diễn 8 vé tại chỗ + 5 vé trực tuyến, buổi đã diễn 7 tại chỗ + 5 trực tuyến.
    private async Task<BoVe> NguoiMuaCo25VeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var buyer = new User { Email = $"b499-{Guid.NewGuid():N}@test.com", FullName = "Nguoi mua 499" };
        db.Users.Add(buyer);
        var tenDaDien = $"Đêm Trịnh {Guid.NewGuid():N}";
        var sap = BuoiHoaNhac($"Sắp diễn {Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(5));
        var da = BuoiHoaNhac(tenDaDien, DateTimeOffset.UtcNow.AddDays(-5));
        db.LoungeShows.AddRange(sap, da);
        await db.SaveChangesAsync();

        async Task<List<Guid>> VeAsync(LoungeShow show, AccessType loai, int soLuong)
        {
            var tier = new TicketTier { LoungeShowId = show.Id, Name = $"Hang {loai}", AccessType = loai };
            db.Add(tier);
            await db.SaveChangesAsync();
            var price = new TicketPrice { TierId = tier.Id, Name = "Gia", Price = 100_000m, SaleStart = DateTimeOffset.UtcNow.AddDays(-30) };
            db.Add(price);
            await db.SaveChangesAsync();
            var ves = Enumerable.Range(0, soLuong).Select(i => new Ticket
            {
                Id = Guid.NewGuid(), // SQLite không có NEWSEQUENTIALID() — khoá Guid phải gán từ code
                BuyerId = buyer.Id, ShowId = show.Id, TierId = tier.Id, PriceId = price.Id,
                Status = TicketStatus.Confirmed, QrCode = $"Q499-{Guid.NewGuid():N}",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            }).ToList();
            db.AddRange(ves);
            await db.SaveChangesAsync();
            return ves.Select(v => v.Id).ToList();
        }

        return new BoVe(buyer.Id,
            await VeAsync(sap, AccessType.Physical, 8), await VeAsync(sap, AccessType.Livestream, 5),
            await VeAsync(da, AccessType.Physical, 7), await VeAsync(da, AccessType.Livestream, 5), tenDaDien);
    }

    private static LoungeShow BuoiHoaNhac(string ten, DateTimeOffset batDau) => new()
    {
        LoungeId = SeedHelper.LoungeId, Name = ten, Description = "test", Format = LoungeShowFormat.Hybrid,
        Status = LoungeShowStatus.Published, ScheduledStart = batDau, ScheduledEnd = batDau.AddHours(2)
    };

    private async Task<(List<Guid> Ids, int Total)> GoiAsync(Guid buyerId, string query)
    {
        var client = _factory.CreateAuthenticatedClient(buyerId, "Audience");
        var res = await client.GetAsync($"/api/v1/tickets/my?{query}");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        return (data.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList(),
                data.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task DaDien_LocTruocKhiPhanTrang_Trang2Va3TraDungPhanConLai()
    {
        var bo = await NguoiMuaCo25VeAsync();

        var (t1, total) = await GoiAsync(bo.BuyerId, "when=Past&page=1&pageSize=5");
        var (t2, _) = await GoiAsync(bo.BuyerId, "when=Past&page=2&pageSize=5");
        var (t3, _) = await GoiAsync(bo.BuyerId, "when=Past&page=3&pageSize=5");

        total.Should().Be(12, "totalCount đếm theo bộ lọc chứ không phải 25 vé của người mua");
        t2.Should().HaveCount(5);
        t3.Should().HaveCount(2);
        t1.Concat(t2).Concat(t3).Should().BeEquivalentTo(bo.DaDien, "ba trang ghép lại đúng tập vé đã diễn, không lẫn vé sắp diễn");
    }

    [Fact]
    public async Task SapDien_KetHopHinhThuc_ChiVeTrucTuyenCuaBuoiSapDien()
    {
        var bo = await NguoiMuaCo25VeAsync();

        var (ids, total) = await GoiAsync(bo.BuyerId, "when=Upcoming&accessType=Livestream&pageSize=100");

        ids.Should().BeEquivalentTo(bo.SapLivestream);
        total.Should().Be(5);
    }

    [Fact]
    public async Task TuKhoa_KhongPhanBietHoaThuong_KeCaChuCoDau()
    {
        var bo = await NguoiMuaCo25VeAsync();

        var (ids, total) = await GoiAsync(bo.BuyerId, $"keyword={Uri.EscapeDataString(bo.TenBuoiDaDien.ToUpperInvariant())}&pageSize=100");

        ids.Should().BeEquivalentTo(bo.DaDien, "gõ ĐÊM TRỊNH hay đêm trịnh đều phải ra cùng một buổi");
        total.Should().Be(12);
    }

    [Fact]
    public async Task KhongTruyenThamSoMoi_TraNhuCu()
    {
        var bo = await NguoiMuaCo25VeAsync();

        var (ids, total) = await GoiAsync(bo.BuyerId, "status=Confirmed&page=1&pageSize=100");

        ids.Should().BeEquivalentTo(bo.TatCa);
        total.Should().Be(25);
    }
}
