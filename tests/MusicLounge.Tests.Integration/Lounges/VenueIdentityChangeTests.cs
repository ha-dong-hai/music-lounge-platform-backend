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

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// MLACP-352. <c>UpdateLounge</c> ghi đè tên và địa chỉ phòng trà mà không báo ai, không lưu vết. Người
/// giữ vé vào cửa của buổi diễn sắp tới không biết địa chỉ mới — có thể tới sai chỗ. Và phòng trà đã được
/// Admin duyệt (MLACP-307) dựa trên chính tên/địa chỉ đó.
///
/// <para>Ticketmaster: <i>"If an event is rescheduled or moved, your tickets ... are still valid"</i>;
/// <i>"the Event Organizer may give you the option to request a refund"</i>. Codebase đã làm đúng
/// khuôn này cho đổi lịch (<c>RescheduleLoungeShow</c>) — đổi địa chỉ đi cùng khuôn.</para>
///
/// <para>Mỗi bài tạo phòng trà riêng: đổi địa chỉ phòng trà dùng chung trong seed sẽ làm hỏng các test
/// khác.</para>
/// </summary>
[Collection("Integration")]
public sealed class VenueIdentityChangeTests
{
    private const string OldStreet = "10 Đồng Khởi";
    private const string NewStreet = "88 Nguyễn Huệ";

    private readonly ApiFactory _factory;

    public VenueIdentityChangeTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int LoungeId, string Name, int OwnerId);

    private async Task<Venue> CreateVenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = freshOwner.Id,
            Name = $"Phòng trà {Guid.NewGuid():N}"[..20],
            Status = LoungeStatus.Approved,
            Address = new VenueAddress
            {
                Street = OldStreet, Ward = "Bến Nghé", District = "1", City = "HCM",
                Latitude = 10.7769, Longitude = 106.7009
            }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return new Venue(lounge.Id, lounge.Name, freshOwner.Id);
    }

    private async Task<int> CreateShowAsync(
        int loungeId,
        LoungeShowFormat format = LoungeShowFormat.Offline,
        LoungeShowStatus status = LoungeShowStatus.Published,
        int startsInHours = 24 * 10,
        int? cancellationDeadlineHours = null,
        decimal? refundPercentage = 80m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow.AddHours(startsInHours);
        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18],
            Format = format,
            Status = status,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(3),
            CancellationAllowed = false,
            CancellationDeadlineHours = cancellationDeadlineHours,
            RefundPercentage = refundPercentage,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<(Guid TicketId, int PaymentId)> AddTicketAsync(
        int showId, int buyerId, AccessType accessType = AccessType.Physical,
        TicketStatus status = TicketStatus.Confirmed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tier = new TicketTier
        {
            LoungeShowId = showId, Name = accessType.ToString(), AccessType = accessType,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = 250_000m,
            PurchaseChannel = PurchaseChannel.Online, SaleStart = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"MLACP352-{Guid.NewGuid():N}"[..30],
            PayerId = buyerId,
            GrossAmount = 250_000m,
            NetAmount = 250_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            TransactionId = $"V{Guid.NewGuid():N}"[..16],
            PaidAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = buyerId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = showId,
            PaymentId = payment.Id,
            Status = status,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Add(ticket);
        await db.SaveChangesAsync();
        return (ticket.Id, payment.Id);
    }

    private Task<HttpResponseMessage> UpdateAsync(
        Venue venue, string? name = null, string street = OldStreet,
        double latitude = 10.7769, double longitude = 106.7009)
        => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner")
            .PutAsJsonAsync($"/api/v1/lounges/{venue.LoungeId}", new
            {
                Name = name ?? venue.Name,
                Description = (string?)null,
                AtmosphereId = (int?)null,
                Street = street,
                Ward = "Bến Nghé",
                District = "1",
                City = "HCM",
                Latitude = latitude,
                Longitude = longitude
            });

    private async Task<List<Notification>> NoticesAsync(NotificationType type, string referenceType, int referenceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.AsNoTracking()
            .Where(n => n.Type == type && n.ReferenceType == referenceType && n.ReferenceId == referenceId.ToString())
            .ToListAsync();
    }

    private async Task<LoungeShow> ShowAsync(int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.LoungeShows.AsNoTracking().SingleAsync(s => s.Id == showId);
    }

    // ── Đổi địa chỉ: người giữ vé vào cửa phải biết ─────────────────────────

    [Fact]
    public async Task DoiDiaChiThiBaoNguoiGiuVeVaoCuaKemDiaChiCuVaMoi()
    {
        var venue = await CreateVenueAsync();
        var showId = await CreateShowAsync(venue.LoungeId);
        await AddTicketAsync(showId, SeedHelper.AudienceId);

        (await UpdateAsync(venue, street: NewStreet)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notice = (await NoticesAsync(NotificationType.EventVenueChanged, "show", showId))
            .Should().ContainSingle(n => n.UserId == SeedHelper.AudienceId,
                "trước đây địa chỉ bị ghi đè lặng lẽ — khách có thể tới sai chỗ").Subject;
        notice.Body.Should().Contain(NewStreet).And.Contain(OldStreet,
            "khách cần thấy đúng cái gì đã đổi, không chỉ 'địa chỉ đã thay đổi'");
    }

    [Fact]
    public async Task DoiDiaChiThiNguoiMuaTruocHuyDuocVaHoanDu_ChinhSachChoNguoiMuaSauGiuNguyen()
    {
        var venue = await CreateVenueAsync();
        var showId = await CreateShowAsync(venue.LoungeId, refundPercentage: 80m);
        var (ticketId, paymentId) = await AddTicketAsync(showId, SeedHelper.AudienceId);

        (await UpdateAsync(venue, street: NewStreet)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ShowAsync(showId)).CancellationAllowed.Should().BeFalse(
            "MLACP-372: chính sách phòng trà cho người mua sau — đã thấy địa chỉ mới — giữ nguyên");

        var cancel = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);
        cancel.IsSuccessStatusCode.Should().BeTrue(
            "một lời báo 'bạn có thể huỷ vé' mà nút huỷ không chạy thì chỉ là lời hứa suông");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var refund = await db.RefundRequests.SingleAsync(r => r.PaymentId == paymentId);
        refund.RefundPercentage.Should().Be(100m,
            "MLACP-372: phòng trà đổi địa chỉ sau khi khách mua — hoàn đủ, không theo mức 80% của phòng trà");
    }

    [Fact]
    public async Task HanHuyKhongConDatDuocThiNguoiMuaTruocVanHuyDuocToiGioDien()
    {
        // Buổi diễn còn 24 giờ, hạn huỷ là 48 giờ trước giờ diễn: hạn đó đã qua ngay lúc đổi địa chỉ. Trước đây hạn bị
        // xoá khỏi buổi diễn — cho mọi người mua. MLACP-372: hạn của phòng trà giữ nguyên, còn người mua trước vẫn huỷ
        // được, tới giờ diễn, và được hoàn đủ.
        var venue = await CreateVenueAsync();
        var showId = await CreateShowAsync(venue.LoungeId, startsInHours: 24, cancellationDeadlineHours: 48);
        var (ticketId, paymentId) = await AddTicketAsync(showId, SeedHelper.AudienceId);

        (await UpdateAsync(venue, street: NewStreet)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ShowAsync(showId)).CancellationDeadlineHours.Should().Be(48);
        (await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
                .PostAsync($"/api/v1/tickets/{ticketId}/cancel", null))
            .IsSuccessStatusCode.Should().BeTrue("hạn đã qua ngay lúc đổi thì cửa sổ kéo tới giờ diễn");

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefundRequests
                .SingleAsync(r => r.PaymentId == paymentId))
            .RefundPercentage.Should().Be(100m);
    }

    // ── Chỉ những ai thật sự phải tới địa chỉ đó ────────────────────────────

    [Fact]
    public async Task ShowHybridThiChiBaoVeVaoCuaKhongBaoVeXemOnline()
    {
        var venue = await CreateVenueAsync();
        var showId = await CreateShowAsync(venue.LoungeId, format: LoungeShowFormat.Hybrid);
        await AddTicketAsync(showId, SeedHelper.AudienceId, AccessType.Physical);
        await AddTicketAsync(showId, SeedHelper.OtherOwnerId, AccessType.Livestream);

        (await UpdateAsync(venue, street: NewStreet)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notices = await NoticesAsync(NotificationType.EventVenueChanged, "show", showId);
        notices.Should().ContainSingle(n => n.UserId == SeedHelper.AudienceId);
        notices.Should().NotContain(n => n.UserId == SeedHelper.OtherOwnerId,
            "người xem online không phải tới phòng trà — địa chỉ mới không đổi gì cho họ");
    }

    [Fact]
    public async Task BuoiDienDaQuaVaVeDaHuyThiKhongBao()
    {
        var venue = await CreateVenueAsync();
        var pastShow = await CreateShowAsync(venue.LoungeId, status: LoungeShowStatus.Ended, startsInHours: -48);
        await AddTicketAsync(pastShow, SeedHelper.AudienceId);
        var futureShow = await CreateShowAsync(venue.LoungeId);
        await AddTicketAsync(futureShow, SeedHelper.AudienceId, status: TicketStatus.Cancelled);

        (await UpdateAsync(venue, street: NewStreet)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await NoticesAsync(NotificationType.EventVenueChanged, "show", pastShow)).Should().BeEmpty();
        (await NoticesAsync(NotificationType.EventVenueChanged, "show", futureShow)).Should().BeEmpty();
    }

    // ── Admin: vết duy nhất của một thay đổi sau khi đã duyệt ───────────────

    [Fact]
    public async Task DoiTenThiChiBaoAdminKemTruocSau()
    {
        var venue = await CreateVenueAsync();
        var showId = await CreateShowAsync(venue.LoungeId);
        await AddTicketAsync(showId, SeedHelper.AudienceId);
        var newName = $"Tên mới {Guid.NewGuid():N}"[..16];

        (await UpdateAsync(venue, name: newName)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await NoticesAsync(NotificationType.EventVenueChanged, "show", showId)).Should().BeEmpty(
            "đổi tên không đổi chỗ khách phải tới");

        var adminNotice = (await NoticesAsync(NotificationType.VenueIdentityChanged, "lounge", venue.LoungeId))
            .Should().ContainSingle(n => n.UserId == SeedHelper.AdminId,
                "hồ sơ được duyệt dựa trên tên/địa chỉ này, và hệ thống không có bảng nhật ký thay đổi nào")
            .Subject;
        adminNotice.Body.Should().Contain(venue.Name).And.Contain(newName);
    }

    [Fact]
    public async Task LuuYNguyenChiKhacHoaThuongHayKhoangTrangThiKhongBaoAi()
    {
        var venue = await CreateVenueAsync();
        var showId = await CreateShowAsync(venue.LoungeId);
        await AddTicketAsync(showId, SeedHelper.AudienceId);

        (await UpdateAsync(venue, street: "  10  đồng khởi ")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await NoticesAsync(NotificationType.EventVenueChanged, "show", showId)).Should().BeEmpty();
        (await NoticesAsync(NotificationType.VenueIdentityChanged, "lounge", venue.LoungeId)).Should().BeEmpty();
        (await ShowAsync(showId)).CancellationAllowed.Should().BeFalse("không có gì thay đổi để mở quyền huỷ");
    }

    [Fact]
    public async Task ChiDoiToaDoThiChiBaoAdmin()
    {
        var venue = await CreateVenueAsync();
        var showId = await CreateShowAsync(venue.LoungeId);
        await AddTicketAsync(showId, SeedHelper.AudienceId);

        (await UpdateAsync(venue, latitude: 10.7800, longitude: 106.7050)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await NoticesAsync(NotificationType.EventVenueChanged, "show", showId)).Should().BeEmpty(
            "địa chỉ bằng chữ không đổi — đây là chỉnh ghim bản đồ");
        (await NoticesAsync(NotificationType.VenueIdentityChanged, "lounge", venue.LoungeId))
            .Should().ContainSingle(n => n.UserId == SeedHelper.AdminId);
    }
}
