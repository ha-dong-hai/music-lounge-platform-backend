using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-622. Doi nghe si sau khi buoi dien da gui duyet / da mo ban. Truoc day chi sua duoc khi con nhap, nen phong tra
/// khong thay duoc ca si om va nguoi mua khong bao gio duoc bao. Nay: bo nghe si khoi buoi dang mo ban phai ghi ly do,
/// nguoi da mua duoc bao va duoc hoan 100% (bat ke chinh sach hoan cua buoi); them nghe si thi khong mo hoan.
/// </summary>
[Collection("Integration")]
public sealed class LineupChangeTests
{
    private readonly ApiFactory _factory;

    public LineupChangeTests(ApiFactory factory) => _factory = factory;

    private sealed record Buoi(Guid OwnerId, Guid ShowId, Guid PerformanceId, Guid PerformerId, Guid TicketId, Guid BuyerId);

    private async Task<Buoi> DungAsync(LoungeShowStatus trangThai = LoungeShowStatus.Published, PerformerRole vai = PerformerRole.Main)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"l622-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        var buyer = new User { Email = $"k622-{Guid.NewGuid():N}@test.com", FullName = "Người mua" };
        db.Users.AddRange(owner, buyer);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = owner.Id, Name = $"Phòng trà {Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", Ward = "Bến Nghé", District = "1", City = "HCM", Latitude = 10.77, Longitude = 106.70 }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddDays(10);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18], Description = "MLACP-622",
            Format = LoungeShowFormat.Offline, Status = trangThai, ScheduledStart = start, ScheduledEnd = start.AddHours(3),
            CancellationAllowed = true, RefundPercentage = 50m, CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        var performer = new Performer { Name = "Ca sĩ Lan Anh", CreatedByUserId = owner.Id };
        db.Add(performer);
        await db.SaveChangesAsync();

        var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id, Role = vai, OrderIndex = 1 };
        db.Add(performance);
        var tier = new TicketTier { LoungeShowId = show.Id, Name = "Ghế thường", AccessType = AccessType.Physical, CreatedAt = DateTime.UtcNow };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = 200_000m, PurchaseChannel = PurchaseChannel.Online,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Add(price);
        await db.SaveChangesAsync();
        var muaLuc = DateTimeOffset.UtcNow.AddDays(-2);
        var payment = new Payment
        {
            OrderId = $"L622-{Guid.NewGuid():N}"[..30], PayerId = buyer.Id, GrossAmount = 200_000m, NetAmount = 200_000m,
            Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
            TransactionId = $"L{Guid.NewGuid():N}"[..16], PaidAt = muaLuc, CreatedAt = muaLuc
        };
        db.Add(payment);
        await db.SaveChangesAsync();
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = buyer.Id, PriceId = price.Id, TierId = tier.Id, ShowId = show.Id,
            PaymentId = payment.Id, Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online, CreatedAt = muaLuc
        };
        db.Add(ticket);
        await db.SaveChangesAsync();
        return new Buoi(owner.Id, show.Id, performance.Id, performer.Id, ticket.Id, buyer.Id);
    }

    private HttpClient Chu(Buoi b) => _factory.CreateAuthenticatedClient(b.OwnerId, "Owner");

    private Task<HttpResponseMessage> XoaAsync(Buoi b, string? lyDo)
        => Chu(b).DeleteAsync($"/api/v1/lounge-shows/{b.ShowId}/performances/{b.PerformanceId}"
            + (lyDo is null ? "" : $"?changeReason={Uri.EscapeDataString(lyDo)}"));

    private async Task<LoungeShow> DocBuoiAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoungeShows.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    [Fact]
    public async Task BoNgheSi_BuoiDangMoBan_ThieuLyDo_BiChan()
    {
        var b = await DungAsync();
        (await XoaAsync(b, null)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await DocBuoiAsync(b.ShowId)).LineupChangedAt.Should().BeNull("bị chặn thì không được ghi gì");
    }

    [Fact]
    public async Task BoNgheSi_BuoiDangMoBan_CoLyDo_BaoNguoiMua_VaHuyVeDuocHoan100()
    {
        var b = await DungAsync();
        var res = await XoaAsync(b, "Ca sĩ Lan Anh bị viêm họng, bác sĩ yêu cầu nghỉ hát một tuần");
        res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());

        var show = await DocBuoiAsync(b.ShowId);
        show.LineupChangedAt.Should().NotBeNull();
        show.LineupChangeNote.Should().Contain("viêm họng");

        // Khach chua mua (khong dang nhap) cung thay tren trang chi tiet — nguoi dang can nhac mua phai biet.
        var chiTiet = await (await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{b.ShowId}")).Content.ReadAsStringAsync();
        chiTiet.Should().Contain("\"lineupChange\"").And.Contain("viêm họng");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tb = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == b.BuyerId && n.Type == NotificationType.LineupChanged).ToListAsync();
            tb.Should().ContainSingle("người giữ vé phải được báo");
            tb[0].Body.Should().Contain("Lan Anh").And.Contain("viêm họng").And.Contain("100%");
        }

        // Chinh sach cua buoi la hoan 50% — nhung nguoi mua TRUOC thay doi duoc hoan 100%.
        (await _factory.CreateAuthenticatedClient(b.BuyerId, "Audience").PostAsync($"/api/v1/tickets/{b.TicketId}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var paymentId = (await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == b.TicketId)).PaymentId;
            (await db.RefundRequests.AsNoTracking().SingleAsync(r => r.PaymentId == paymentId)).AmountRequested
                .Should().Be(200_000m, "nghệ sĩ đã công bố không còn diễn — hoàn 100%, không theo chính sách 50% của buổi");
        }
    }

    [Fact]
    public async Task ThemNgheSi_BuoiDangMoBan_DuocPhep_KhongMoHoan()
    {
        var b = await DungAsync();
        var res = await Chu(b).PostAsJsonAsync($"/api/v1/lounge-shows/{b.ShowId}/performances",
            new { PerformerId = (Guid?)null, PerformerName = $"Khách mời {Guid.NewGuid():N}"[..20], Role = "Guest", OrderIndex = 2, SetTime = (TimeOnly?)null, AcceptsDonation = true });
        res.StatusCode.Should().Be(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        (await DocBuoiAsync(b.ShowId)).LineupChangedAt.Should().BeNull("thêm nghệ sĩ không bất lợi cho người đã mua");
    }

    [Fact]
    public async Task HaNgheSiChinh_BuoiDangMoBan_LaThayDoiBatLoi()
    {
        var b = await DungAsync();
        var url = $"/api/v1/lounge-shows/{b.ShowId}/performances/{b.PerformanceId}";
        (await Chu(b).PutAsJsonAsync(url, new { Role = "Guest", OrderIndex = 1, SetTime = (TimeOnly?)null, AcceptsDonation = true }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "hạ nghệ sĩ chính phải ghi lý do");
        (await Chu(b).PutAsJsonAsync(url, new { Role = "Guest", OrderIndex = 1, SetTime = (TimeOnly?)null, AcceptsDonation = true,
            ChangeReason = "Ca sĩ chỉ hát được hai bài vì lịch quay truyền hình" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DocBuoiAsync(b.ShowId)).LineupChangedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DoiThuTu_KhongPhaiThayDoiBatLoi()
    {
        var b = await DungAsync();
        (await Chu(b).PutAsJsonAsync($"/api/v1/lounge-shows/{b.ShowId}/performances/{b.PerformanceId}",
            new { Role = "Main", OrderIndex = 3, SetTime = (TimeOnly?)new TimeOnly(21, 30), AcceptsDonation = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DocBuoiAsync(b.ShowId)).LineupChangedAt.Should().BeNull();
    }

    [Fact]
    public async Task BuoiDangDien_KhongSuaDuoc()
    {
        var b = await DungAsync(LoungeShowStatus.Ongoing);
        (await XoaAsync(b, "Ca sĩ về sớm vì có việc gia đình đột xuất")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task BanNhap_XoaKhongCanLyDo_NhuTruoc()
    {
        var b = await DungAsync(LoungeShowStatus.Draft);
        (await XoaAsync(b, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TietMucDaNhanUngHo_KhongXoaDuoc_409()
    {
        var b = await DungAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new Donation { PerformanceId = b.PerformanceId, DonorUserId = b.BuyerId, Gross = 100_000m, Net = 90_000m, Status = DonationStatus.PendingPayment });
            await db.SaveChangesAsync();
        }
        var res = await XoaAsync(b, "Ca sĩ Lan Anh bị viêm họng, phải nghỉ");
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("tắt nhận ủng hộ",
            "chủ phòng trà phải biết vì sao không xoá được và nên làm gì — không phải câu chung của lỗi cơ sở dữ liệu");
        (await DocBuoiAsync(b.ShowId)).LineupChangedAt.Should().BeNull("bị chặn thì không mở cửa sổ hoàn, không báo ai");
    }
}
