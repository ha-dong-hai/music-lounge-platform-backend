using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-457. Frontend gửi <c>city</c>, <c>minPrice</c>, <c>maxPrice</c>, <c>includeSoldOut</c> vào
/// <c>GET /lounge-shows/search</c> từ lâu, nhưng query không nhận chúng — ASP.NET bỏ qua tham số lạ không báo lỗi, nên
/// người dùng chọn bộ lọc xong thấy kết quả y hệt, không lỗi, không lời giải thích.
///
/// Riêng lọc giá không nối thẳng lên được vì tầng dưới sai hai chỗ: min và max xét trên hai mức giá khác nhau, và phép
/// lọc tính cả mức giá chưa duyệt trong khi khoảng giá hiện trên thẻ chỉ tính giá đã duyệt (MLACP-388).
/// </summary>
[Collection("Integration")]
public sealed class SearchFilterTests
{
    private readonly ApiFactory _factory;

    public SearchFilterTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Paged<T>(IReadOnlyList<T> Items, int TotalCount);
    private sealed record Item(int Id, string Name);

    /// <summary>Phòng trà riêng cho mỗi bài, thành phố duy nhất — để bài này không thấy dữ liệu của bài khác.</summary>
    private async Task<(int LoungeId, string City)> PhongTraRiengAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"search-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var city = $"TP-{Guid.NewGuid():N}"[..18];
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"SearchVenue-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", Ward = "P1", District = "", City = city }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, city);
    }

    /// <summary>Buổi hòa nhạc đã công khai, kèm các mức giá mô tả bằng (giá, đã duyệt chưa, số vé tối đa).</summary>
    private async Task<int> BuoiHoaNhacAsync(
        int loungeId, params (decimal Gia, bool DaDuyet, int? SoVe)[] mucGia)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var start = DateTimeOffset.UtcNow.AddDays(10);
        var show = new LoungeShow
        {
            LoungeId = loungeId, Name = $"SearchShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        if (mucGia.Length > 0)
        {
            var tier = new TicketTier
            {
                LoungeShowId = show.Id, Name = "Standard", AccessType = AccessType.Physical, TotalCapacity = 1000
            };
            db.Add(tier);
            await db.SaveChangesAsync();

            foreach (var (gia, daDuyet, soVe) in mucGia)
                db.Add(new TicketPrice
                {
                    TierId = tier.Id, Name = $"Muc {gia:0}", Price = gia, IsActive = daDuyet, Quota = soVe,
                    PurchaseChannel = PurchaseChannel.Online,
                    SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
                    SaleEnd = start
                });
            await db.SaveChangesAsync();
        }

        return show.Id;
    }

    /// <summary>Bán hết vé của mọi mức giá thuộc buổi hòa nhạc này.</summary>
    private async Task BanHetVeAsync(int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var prices = db.Set<TicketPrice>().Where(p => p.Tier.LoungeShowId == showId).ToList();
        foreach (var price in prices)
            for (var i = 0; i < (price.Quota ?? 0); i++)
                db.Add(new Ticket
                {
                    Id = Guid.NewGuid(), PriceId = price.Id, TierId = price.TierId, ShowId = showId,
                    Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online,
                    CreatedAt = DateTimeOffset.UtcNow
                });
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<int>> TimAsync(string query)
    {
        var res = await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/search?{query}&pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        var body = await res.Content.ReadFromJsonAsync<Envelope<Paged<Item>>>();
        return body!.Data.Items.Select(i => i.Id).ToList();
    }

    // ---------- lọc theo tỉnh/thành ----------

    [Fact]
    public async Task LocTheoTinh_ChiTraBuoiHoaNhacCuaTinhDo()
    {
        var (loungeA, cityA) = await PhongTraRiengAsync();
        var (loungeB, _) = await PhongTraRiengAsync();
        var showA = await BuoiHoaNhacAsync(loungeA);
        var showB = await BuoiHoaNhacAsync(loungeB);

        var ketQua = await TimAsync($"city={Uri.EscapeDataString(cityA)}");

        ketQua.Should().Contain(showA);
        ketQua.Should().NotContain(showB, "chọn một tỉnh mà vẫn thấy buổi diễn tỉnh khác thì bộ lọc vô nghĩa");
    }

    // ---------- lọc theo giá ----------

    [Fact]
    public async Task LocTheoGia_KhongKhopBuoiHoaNhacChiCoVeNgoaiKhoang()
    {
        var (lounge, city) = await PhongTraRiengAsync();
        var haiCuc = await BuoiHoaNhacAsync(lounge, (100_000m, true, null), (1_000_000m, true, null));
        var trongKhoang = await BuoiHoaNhacAsync(lounge, (450_000m, true, null));

        var ketQua = await TimAsync($"city={Uri.EscapeDataString(city)}&minPrice=400000&maxPrice=500000");

        ketQua.Should().Contain(trongKhoang);
        ketQua.Should().NotContain(haiCuc,
            "vé 100.000đ và vé 1.000.000đ: không có vé nào nằm trong khoảng 400.000–500.000đ");
    }

    [Fact]
    public async Task LocTheoGia_BoQuaMucGiaChuaDuyet()
    {
        var (lounge, city) = await PhongTraRiengAsync();
        var show = await BuoiHoaNhacAsync(lounge, (1_000_000m, true, null), (100_000m, false, null));

        var ketQua = await TimAsync($"city={Uri.EscapeDataString(city)}&maxPrice=200000");

        ketQua.Should().NotContain(show,
            "giá chưa duyệt không phải giá đang bán — khoảng giá trên thẻ cũng không tính nó (MLACP-388)");
    }

    [Fact]
    public async Task ChiGuiMotDauKhoangGia_DauConLaiKhongBiChan()
    {
        var (lounge, city) = await PhongTraRiengAsync();
        var re = await BuoiHoaNhacAsync(lounge, (100_000m, true, null));
        var dat = await BuoiHoaNhacAsync(lounge, (900_000m, true, null));

        var tuNuaTrieu = await TimAsync($"city={Uri.EscapeDataString(city)}&minPrice=500000");
        var toiNuaTrieu = await TimAsync($"city={Uri.EscapeDataString(city)}&maxPrice=500000");

        tuNuaTrieu.Should().Contain(dat).And.NotContain(re);
        toiNuaTrieu.Should().Contain(re).And.NotContain(dat);
    }

    [Theory]
    [InlineData("minPrice=500000&maxPrice=100000", "giá cao nhất nhỏ hơn giá thấp nhất")]
    [InlineData("minPrice=-5", "giá âm")]
    [InlineData("minPrice=400000.5", "giá có phần lẻ — VND không có đơn vị lẻ")]
    public async Task GiaVoNghia_Tra400_ChuKhongImLangTraRong(string query, string vi)
    {
        var res = await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/search?{query}");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"{vi}: trả danh sách rỗng sẽ làm người dùng tưởng hết buổi diễn, trong khi họ chỉ gõ nhầm");
    }

    // ---------- còn vé ----------

    [Fact]
    public async Task ConVe_LoaiBuoiHoaNhacDaBanHet()
    {
        var (lounge, city) = await PhongTraRiengAsync();
        var conVe = await BuoiHoaNhacAsync(lounge, (200_000m, true, 10));
        var hetVe = await BuoiHoaNhacAsync(lounge, (200_000m, true, 2));
        await BanHetVeAsync(hetVe);

        var tatCa = await TimAsync($"city={Uri.EscapeDataString(city)}");
        var chiConVe = await TimAsync($"city={Uri.EscapeDataString(city)}&includeSoldOut=false");

        tatCa.Should().Contain(hetVe, "tiền đề: mặc định vẫn hiện buổi đã hết vé");
        chiConVe.Should().Contain(conVe).And.NotContain(hetVe);
    }

    [Fact]
    public async Task ConVe_ConMucGiaKhongGioiHanSoVe_ThiVanConVe()
    {
        var (lounge, city) = await PhongTraRiengAsync();
        // Hạng thường có 2 vé (sẽ bán hết) + hạng đứng xem không đặt giới hạn.
        var show = await BuoiHoaNhacAsync(lounge, (200_000m, true, 2), (50_000m, true, null));
        await BanHetVeAsync(show);

        var chiConVe = await TimAsync($"city={Uri.EscapeDataString(city)}&includeSoldOut=false");

        chiConVe.Should().Contain(show,
            "hạng thường hết vé nhưng hạng đứng xem không giới hạn — vẫn còn vé để bán");
    }

    [Fact]
    public async Task ConVe_BuoiHoaNhacChuaCoGiaDuyet_KhongBiCoiLaHetVe()
    {
        var (lounge, city) = await PhongTraRiengAsync();
        var chuaCoGia = await BuoiHoaNhacAsync(lounge);
        var chiCoGiaChuaDuyet = await BuoiHoaNhacAsync(lounge, (200_000m, false, 5));

        var chiConVe = await TimAsync($"city={Uri.EscapeDataString(city)}&includeSoldOut=false");

        chiConVe.Should().Contain(chuaCoGia).And.Contain(chiCoGiaChuaDuyet,
            "thiếu thông tin không phải bằng chứng (MLACP-327): buổi vừa đăng chưa kịp cấu hình vé " +
            "không phải là buổi đã bán hết");
    }
}
