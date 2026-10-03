using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// MLACP-546. Trang Vé của tôi in "kỷ niệm đêm đã đến" (ảnh Polaroid) — cần biết khách ĐÃ THẬT SỰ có mặt lúc nào và ảnh
/// của buổi, ngay trong danh sách (không gọi chi tiết từng vé = N+1). TicketListItemDto trước đây không có hai thứ này.
/// </summary>
[Collection("Integration")]
public sealed class MyTicketsAttendedTests
{
    private readonly ApiFactory _factory;
    public MyTicketsAttendedTests(ApiFactory factory) => _factory = factory;

    private async Task<(Guid Buyer, Guid VeDaDen, Guid VeChuaDen, Guid VeBuoiKhongAnh, DateTimeOffset GioDen)> DungAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = OrderedGuid.New();
        db.Users.Add(new User { Id = id, Email = $"b546-{id}@test.com", FullName = "Nguoi mua 546" });
        db.Lounges.Add(new MusicLoungeVenue { Id = id, OwnerId = id, Name = $"PT 546 {id}",
            PrimaryImageUrl = "/uploads/phong-tra-546.jpg", Address = new VenueAddress { Street = "1", District = "1", City = "HCM" } });
        var coAnh = new LoungeShow { Id = OrderedGuid.New(), LoungeId = id, Name = "Đêm có ảnh", Description = "x",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ended, CoverImageUrl = "/uploads/bia-546.jpg",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-3), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(-3).AddHours(2) };
        var khongAnh = new LoungeShow { Id = OrderedGuid.New(), LoungeId = id, Name = "Đêm không ảnh", Description = "x",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ended, CoverImageUrl = null,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-2), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(-2).AddHours(2) };
        db.LoungeShows.AddRange(coAnh, khongAnh);
        await db.SaveChangesAsync();

        async Task<Guid> VeAsync(LoungeShow show, DateTimeOffset? vaoCua)
        {
            var tier = new TicketTier { LoungeShowId = show.Id, Name = "Ghế", AccessType = AccessType.Physical };
            db.Add(tier); await db.SaveChangesAsync();
            var price = new TicketPrice { TierId = tier.Id, Name = "Giá", Price = 200_000m, SaleStart = DateTimeOffset.UtcNow.AddDays(-30) };
            db.Add(price); await db.SaveChangesAsync();
            var ve = new Ticket { Id = Guid.NewGuid(), BuyerId = id, ShowId = show.Id, TierId = tier.Id, PriceId = price.Id,
                Status = vaoCua.HasValue ? TicketStatus.Used : TicketStatus.Confirmed, QrCode = $"Q546-{Guid.NewGuid():N}",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10) };
            db.Add(ve);
            db.Add(new PhysicalTicketDetail { TicketId = ve.Id, CheckedInAt = vaoCua });
            await db.SaveChangesAsync();
            return ve.Id;
        }

        var gioDen = DateTimeOffset.UtcNow.AddDays(-3).AddMinutes(15);
        return (id, await VeAsync(coAnh, gioDen), await VeAsync(coAnh, null), await VeAsync(khongAnh, gioDen.AddDays(1)), gioDen);
    }

    [Fact]
    public async Task MyTickets_ReturnsAttendedAt_AndShowImageWithLoungeFallback()
    {
        var bo = await DungAsync();
        var res = await _factory.CreateAuthenticatedClient(bo.Buyer, "Audience").GetAsync("/api/v1/tickets/my?pageSize=50");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var doc = JsonDocument.Parse(body);
        var ves = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .ToDictionary(v => v.GetProperty("id").GetGuid(), v => v.Clone());

        var daDen = ves[bo.VeDaDen];
        daDen.GetProperty("attendedAt").GetDateTimeOffset().Should().BeCloseTo(bo.GioDen, TimeSpan.FromSeconds(1));
        daDen.GetProperty("showImageUrl").GetString().Should().Be("/uploads/bia-546.jpg");

        ves[bo.VeChuaDen].GetProperty("attendedAt").ValueKind.Should().Be(JsonValueKind.Null, "chưa quét vé thì chưa 'đã đến'");

        ves[bo.VeBuoiKhongAnh].GetProperty("showImageUrl").GetString()
            .Should().Be("/uploads/phong-tra-546.jpg", "buổi chưa có ảnh bìa thì dùng ảnh phòng trà, như đầu trang buổi diễn");
    }
}
