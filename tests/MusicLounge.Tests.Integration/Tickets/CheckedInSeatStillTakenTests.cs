using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// MLACP-459. Soát vé xong thì chỗ đó vẫn có người ngồi — nhưng hệ thống lại coi như trống.
///
/// Phép đếm chỗ đã chiếm (<c>GetReservedQuantitiesByPriceIdsAsync</c>) chỉ tính vé <c>Confirmed</c> và <c>Pending</c>.
/// Soát vé vào cửa chuyển vé sang <c>Used</c>, nên mỗi lượt soát làm con số "đã bán" tụt đi một. Quầy vé lại bán được
/// TRONG LÚC buổi diễn đang chạy — BR-31 cho bán tới trước giờ kết thúc 60 phút — nên hai cửa sổ này chồng nhau: soát 30
/// vé thì bán vượt được 30 vé, vào đúng đêm diễn đông khách nhất.
///
/// Đây là lỗi tiền và sức chứa thật: khán giả trả tiền cho một chỗ không tồn tại.
/// </summary>
[Collection("Integration")]
public sealed class CheckedInSeatStillTakenTests
{
    private readonly ApiFactory _factory;

    public CheckedInSeatStillTakenTests(ApiFactory factory) => _factory = factory;

    private HttpClient NhanVien() =>
        _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

    /// <summary>
    /// Buổi hòa nhạc ĐANG DIỄN, còn hơn 60 phút nữa mới kết thúc (nên quầy vẫn được bán theo BR-31), với một đợt bán
    /// đúng <paramref name="soVe"/> vé.
    /// </summary>
    private async Task<int> BuoiDienDangDienAsync(int soVe)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"BanVuot-{Guid.NewGuid():N}",
            Description = "test",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Ongoing,
            ScheduledStart = DateTimeOffset.UtcNow.AddHours(-1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Door", AccessType = AccessType.Physical, TotalCapacity = soVe,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Standard", Price = 150_000m, Quota = soVe,
            PurchaseChannel = PurchaseChannel.Both,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddHours(3)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        return price.Id;
    }

    private async Task<HttpResponseMessage> BanTaiQuayAsync(int priceId, int soLuong = 1)
        => await NhanVien().PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = priceId, Quantity = soLuong });

    private static async Task<IReadOnlyList<string>> MaQrAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("tickets").EnumerateArray()
            .Select(t => t.GetProperty("qrCode").GetString()!)
            .ToList();
    }

    private async Task SoatVeAsync(string qrCode)
    {
        var res = await NhanVien().PostAsJsonAsync("/api/v1/tickets/check-in", new { QrCode = qrCode });
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SoatVeXong_ChoDoVanPhaiTinhLaDaChiem_KhongBanThemDuoc()
    {
        var priceId = await BuoiDienDangDienAsync(soVe: 2);

        var daBan = await BanTaiQuayAsync(priceId, 2);
        daBan.StatusCode.Should().Be(HttpStatusCode.Created, await daBan.Content.ReadAsStringAsync());

        (await BanTaiQuayAsync(priceId)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "tiền đề: bán hết 2/2 vé thì vé thứ ba phải bị từ chối");

        var qr = await MaQrAsync(daBan);
        await SoatVeAsync(qr[0]);

        var sauKhiSoat = await BanTaiQuayAsync(priceId);

        sauKhiSoat.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "khán giả đã vào cửa và đang ngồi trong phòng — chỗ đó không hề trống ra");
    }

    [Fact]
    public async Task SoatHetCaPhong_VanKhongBanThemDuocVeNao()
    {
        var priceId = await BuoiDienDangDienAsync(soVe: 3);
        var daBan = await BanTaiQuayAsync(priceId, 3);
        var qr = await MaQrAsync(daBan);

        foreach (var ma in qr) await SoatVeAsync(ma);

        var sauKhiSoatHet = await BanTaiQuayAsync(priceId, 3);

        sauKhiSoatHet.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "soát hết phòng mà bán lại được đúng bằng sức chứa là bán gấp đôi số chỗ có thật");
    }

    /// <summary>
    /// Vé <c>Refunded</c> = vé ĐÃ DÙNG rồi mới được hoàn (buổi phát bị cắt ngang, hoặc khiếu nại được chấp nhận sau khi
    /// khách đã vào) — xem chú thích trong <c>TicketStatus</c> và hai chỗ đặt trạng thái này, đều theo mẫu
    /// <c>Status == Used ? Refunded : Cancelled</c>. Người đó đã ngồi trong phòng, nên chỗ vẫn bị chiếm y như
    /// <c>Used</c>. Khác hẳn <c>Cancelled</c> (chưa từng dùng) — chỗ đó mới thật sự trống ra.
    /// </summary>
    [Fact]
    public async Task VeDaDungRoiDuocHoanTien_VanChiemCho_ConVeHuyThiKhongChiem()
    {
        var priceId = await BuoiDienDangDienAsync(soVe: 2);
        var daBan = await BanTaiQuayAsync(priceId, 2);
        var qr = await MaQrAsync(daBan);
        await SoatVeAsync(qr[0]);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Sắp xếp ở phía ứng dụng: SQLite không ORDER BY được kiểu DateTimeOffset.
            var ve = (await db.Tickets.Where(t => t.PriceId == priceId).ToListAsync())
                .OrderByDescending(t => t.Status == TicketStatus.Used)
                .ToList();
            ve[0].Status.Should().Be(TicketStatus.Used, "tiền đề: vé đầu đã được soát");
            ve[0].Status = TicketStatus.Refunded;   // đã dùng rồi mới hoàn
            ve[1].Status = TicketStatus.Cancelled;  // chưa từng dùng — chỗ này trống thật
            await db.SaveChangesAsync();
        }

        var res = await BanTaiQuayAsync(priceId);

        res.StatusCode.Should().Be(HttpStatusCode.Created,
            "chỉ có đúng một chỗ trống ra (vé bị huỷ), nên bán thêm được đúng một vé");

        (await BanTaiQuayAsync(priceId)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "chỗ của vé đã dùng rồi hoàn tiền vẫn có người ngồi — không được bán tiếp");
    }
}
