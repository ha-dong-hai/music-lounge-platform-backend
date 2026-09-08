using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Livestreams;

/// <summary>
/// MLACP-303. Two columns read into a DTO that nothing ever wrote — the same shape of defect as
/// CoverImageUrl in MLACP-300, found by the same sweep.
///
/// Livestream.PeakViewerCount and TotalViews meant the owner's livestream analytics page reported
/// zero viewers for every broadcast that ever happened. The live count was being maintained all
/// along; only the two lifetime figures were dropped on the floor.
///
/// The counter logic now lives in the repository rather than inside the SignalR hub, because a hub
/// is close to untestable here and "a column nobody writes" is exactly the kind of bug that only a
/// test catches — the page does not break, it just quietly says 0.
/// </summary>
[Collection("Integration")]
public sealed class ViewerStatsAndSeatInfoTests
{
    private readonly ApiFactory _factory;

    public ViewerStatsAndSeatInfoTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedLivestreamAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"StatsShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Online,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(2)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var livestream = new Livestream { LoungeShowId = show.Id, Status = LivestreamStatus.Live };
        db.Add(livestream);
        await db.SaveChangesAsync();
        return livestream.Id;
    }

    private async Task<(int Viewers, int Peak, int Total)> ReadAsync(int livestreamId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var l = await db.Set<Livestream>().AsNoTracking().SingleAsync(x => x.Id == livestreamId);
        return (l.ViewerCount, l.PeakViewerCount, l.TotalViews);
    }

    private async Task<int> JoinAsync(int livestreamId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ILivestreamRepository>()
            .RecordViewerJoinedAsync(livestreamId);
    }

    private async Task<int> LeaveAsync(int livestreamId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ILivestreamRepository>()
            .RecordViewerLeftAsync(livestreamId);
    }

    [Fact]
    public async Task ViewersJoining_RaiseTheLiveCount_ThePeak_AndTheLifetimeTotal()
    {
        var id = await SeedLivestreamAsync();

        (await JoinAsync(id)).Should().Be(1);
        (await JoinAsync(id)).Should().Be(2);
        (await JoinAsync(id)).Should().Be(3);

        var (viewers, peak, total) = await ReadAsync(id);
        viewers.Should().Be(3);
        peak.Should().Be(3);
        total.Should().Be(3);
    }

    [Fact]
    public async Task ThePeakSurvivesEveryoneLeaving()
    {
        // The figure the analytics page is actually for: how many watched at the busiest moment,
        // not how many are left at the end.
        var id = await SeedLivestreamAsync();
        await JoinAsync(id);
        await JoinAsync(id);
        await JoinAsync(id);

        await LeaveAsync(id);
        await LeaveAsync(id);
        await LeaveAsync(id);

        var (viewers, peak, total) = await ReadAsync(id);
        viewers.Should().Be(0);
        peak.Should().Be(3, "the peak is a fact about the broadcast, not about right now");
        total.Should().Be(3, "and so is the total — people leaving does not un-watch it");
    }

    [Fact]
    public async Task RejoiningCountsAsAnotherView_ButDoesNotInflateThePeak()
    {
        var id = await SeedLivestreamAsync();
        await JoinAsync(id);
        await LeaveAsync(id);
        await JoinAsync(id);

        var (viewers, peak, total) = await ReadAsync(id);
        viewers.Should().Be(1);
        peak.Should().Be(1, "there was never more than one person watching at once");
        total.Should().Be(2);
    }

    [Fact]
    public async Task TheLiveCountNeverGoesNegative()
    {
        // A connection rejected at the access check still fires the disconnect path.
        var id = await SeedLivestreamAsync();

        (await LeaveAsync(id)).Should().Be(0);
        (await LeaveAsync(id)).Should().Be(0);

        (await ReadAsync(id)).Viewers.Should().Be(0);
    }

    [Fact]
    public async Task TheOwnersAnalyticsPage_ReportsWhatActuallyHappened()
    {
        // The end the whole fix exists for: before this, every row on this page read zero.
        var id = await SeedLivestreamAsync();
        await JoinAsync(id);
        await JoinAsync(id);
        await LeaveAsync(id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var live = await db.Set<Livestream>().AsNoTracking().SingleAsync(x => x.Id == id);

        live.PeakViewerCount.Should().Be(2);
        live.TotalViews.Should().Be(2);
    }

    // ---------- chỗ ngồi ----------

    [Fact]
    public async Task ScanningATicketAtTheDoor_ShowsWhichAreaToSeatThemIn()
    {
        // Tickets are sold by area, not by numbered seat, so the area name is the only seating
        // information that exists — and SeatInfo, the column the DTO reads, is written by nothing.
        // Staff greeting a guest had a blank field exactly where they needed an answer.
        const string zoneName = "Khu VIP tầng 2";
        string qr;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var zone = new SeatingZone
            {
                LoungeId = SeedHelper.LoungeId, Name = zoneName, Capacity = 20
            };
            db.Add(zone);

            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"SeatShow-{Guid.NewGuid():N}",
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(2),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(2).AddHours(3)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();

            var tier = new TicketTier
            {
                LoungeShowId = show.Id, Name = "VIP",
                AccessType = AccessType.Physical, TotalCapacity = 20, ZoneId = zone.Id
            };
            db.Add(tier);
            await db.SaveChangesAsync();

            var price = new TicketPrice
            {
                TierId = tier.Id, Name = "Regular", Price = 300_000m, Quota = 20, IsActive = true,
                SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
                SaleEnd = DateTimeOffset.UtcNow.AddDays(1),
                PurchaseChannel = PurchaseChannel.Online
            };
            db.Add(price);
            await db.SaveChangesAsync();

            var ticket = new Ticket
            {
                // Đặt Id tay: cột này mặc định NEWSEQUENTIALID() của SQL Server, provider SQLite
                // dùng trong test không có hàm đó.
                Id = Guid.NewGuid(),
                ShowId = show.Id,
                TierId = tier.Id,
                PriceId = price.Id,
                BuyerId = SeedHelper.AudienceId,
                Status = TicketStatus.Confirmed,
                QrCode = $"QR-{Guid.NewGuid():N}",
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(ticket);
            await db.SaveChangesAsync();

            db.Add(new PhysicalTicketDetail { TicketId = ticket.Id });
            await db.SaveChangesAsync();
            qr = ticket.QrCode;
        }

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner")
            .GetAsync($"/api/v1/tickets/by-qr/{qr}");
        res.IsSuccessStatusCode.Should().BeTrue();

        (await res.Content.ReadAsStringAsync()).Should().Contain(zoneName,
            "the point of scanning the ticket is knowing where to send the guest");
    }
}
