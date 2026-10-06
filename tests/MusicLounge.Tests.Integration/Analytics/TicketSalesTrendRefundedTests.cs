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

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-689. Buổi có vé đã bán rồi được hoàn tiền / khách huỷ thì doanh số đúng là 0, nhưng "Tiến độ bán vé" chỉ nói
/// "chưa có ngày nào bán được vé" — chủ phòng trà tưởng không ai mua (gặp thật trên Azure 06/10: buổi phát bị cắt ngang,
/// vé được hoàn tự động). API trả thêm số vé đã hoàn và đã huỷ; hai số này KHÔNG được lẫn vào số đã bán.
/// </summary>
[Collection("Integration")]
public sealed class TicketSalesTrendRefundedTests
{
    private readonly ApiFactory _factory;

    public TicketSalesTrendRefundedTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope(bool Success, Trend Data);
    private sealed record Trend(int TotalTicketsSold, decimal TotalRevenue, int TicketsRefunded, int TicketsCancelled);

    private async Task<(Guid OwnerId, Guid LoungeId, Guid ShowId)> SeedAsync(params TicketStatus[] cacVe)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"trend-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Trend-{Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"TrendShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Online, Status = LoungeShowStatus.Ended, ScheduledStart = start, ScheduledEnd = start.AddHours(2)
        };
        db.Add(show);
        await db.SaveChangesAsync();
        var tier = new TicketTier { LoungeShowId = show.Id, Name = "Online", AccessType = AccessType.Livestream, TotalCapacity = 100 };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Vé thường", Price = 150_000m, Quota = 100, IsActive = true,
            SaleStart = start.AddDays(-10), SaleEnd = start.AddHours(-1), PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();
        foreach (var tt in cacVe)
            db.Add(new Ticket
            {
                Id = Guid.NewGuid(), ShowId = show.Id, TierId = tier.Id, PriceId = price.Id, BuyerId = SeedHelper.AudienceId,
                Status = tt, QrCode = $"QR-{Guid.NewGuid():N}", PurchaseChannel = PurchaseChannel.Online, CreatedAt = start.AddDays(-2)
            });
        await db.SaveChangesAsync();
        return (owner.Id, lounge.Id, show.Id);
    }

    private async Task<Trend> TrendAsync((Guid OwnerId, Guid LoungeId, Guid ShowId) s)
    {
        var res = await _factory.CreateAuthenticatedClient(s.OwnerId, "Owner", s.LoungeId)
            .GetAsync($"/api/v1/analytics/shows/{s.ShowId}/ticket-sales-trend");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope>())!.Data;
    }

    [Fact]
    public async Task VeDaHoanVaDaHuy_DuocDemRieng_KhongLanVaoSoDaBan()
    {
        var t = await TrendAsync(await SeedAsync(
            TicketStatus.Confirmed, TicketStatus.Used, TicketStatus.Refunded, TicketStatus.Refunded, TicketStatus.Cancelled));

        t.TotalTicketsSold.Should().Be(2, "chỉ vé Confirmed + Used là đã bán");
        t.TotalRevenue.Should().Be(300_000m);
        t.TicketsRefunded.Should().Be(2);
        t.TicketsCancelled.Should().Be(1);
    }

    [Fact]
    public async Task BuoiBiHoanHet_SoDaBanBang0_NhungVanBietDaTungBan()
    {
        // Đúng ca thật trên Azure: 1 vé hoàn (buổi phát bị cắt ngang) + 1 vé khách huỷ.
        var t = await TrendAsync(await SeedAsync(TicketStatus.Refunded, TicketStatus.Cancelled));

        t.TotalTicketsSold.Should().Be(0);
        t.TicketsRefunded.Should().Be(1);
        t.TicketsCancelled.Should().Be(1);
    }
}
