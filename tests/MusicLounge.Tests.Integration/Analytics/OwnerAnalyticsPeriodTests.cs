using System.Text.Json;
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
/// MLACP-659. Trang "Báo cáo doanh thu" của chủ phòng trà không có bộ lọc thời gian: API tổng quan (my-lounge) luôn tính
/// mọi thời gian và xu hướng cố định 6 tháng. Nay nhận from/to, lọc theo lúc mua vé — cùng quy tắc với revenue-report.
/// Mỗi bài một phòng trà riêng, không đụng dữ liệu seed dùng chung.
/// </summary>
[Collection("Integration")]
public sealed class OwnerAnalyticsPeriodTests
{
    private readonly ApiFactory _factory;

    public OwnerAnalyticsPeriodTests(ApiFactory factory) => _factory = factory;

    /// <summary>Một buổi diễn, giá vé 200.000đ, một vé mua 03/2026 và một vé mua 08/2026.</summary>
    private async Task<(Guid OwnerId, Guid LoungeId)> TwoTicketsInDifferentMonthsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"owner659-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà 659", Role = UserRole.Owner };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue659-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        var start = new DateTimeOffset(2026, 9, 1, 13, 0, 0, TimeSpan.Zero);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = "Đêm nhạc 659", Status = LoungeShowStatus.Ended,
            ScheduledStart = start, ScheduledEnd = start.AddHours(2), CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        var tier = new TicketTier { LoungeShowId = show.Id, Name = "Ghế thường", AccessType = AccessType.Physical, TotalCapacity = 50 };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Giá thường", Price = 200_000m, Quota = 50,
            SaleStart = start.AddMonths(-7), PurchaseChannel = PurchaseChannel.Both
        };
        db.Add(price);
        await db.SaveChangesAsync();
        foreach (var boughtAt in new[] { new DateTimeOffset(2026, 3, 10, 5, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 10, 5, 0, 0, TimeSpan.Zero) })
            db.Add(new Ticket
            {
                Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = price.Id, TierId = tier.Id, ShowId = show.Id,
                Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online, CreatedAt = boughtAt
            });
        await db.SaveChangesAsync();
        return (owner.Id, lounge.Id);
    }

    private async Task<JsonElement> MyLoungeAsync(Guid ownerId, Guid loungeId, string range = "")
    {
        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .GetAsync($"/api/v1/analytics/my-lounge?loungeId={loungeId}{range}");
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task APeriod_CountsOnlyTicketsBoughtInsideIt()
    {
        var (ownerId, loungeId) = await TwoTicketsInDifferentMonthsAsync();

        var all = await MyLoungeAsync(ownerId, loungeId);
        all.GetProperty("totalTicketsSold").GetInt32().Should().Be(2, "test premise: no period = all time");

        var aug = await MyLoungeAsync(ownerId, loungeId, "&from=2026-08-01T00:00:00%2B07:00&to=2026-08-31T23:59:59%2B07:00");
        aug.GetProperty("totalTicketsSold").GetInt32().Should().Be(1, "only the August purchase falls inside the period");
        aug.GetProperty("ticketRevenue").GetDecimal().Should().Be(200_000m);
        aug.GetProperty("topShows").EnumerateArray().Single().GetProperty("revenue").GetDecimal().Should().Be(200_000m);
    }

    [Fact]
    public async Task ThePeriod_DecidesWhichMonthsTheTrendShows()
    {
        var (ownerId, loungeId) = await TwoTicketsInDifferentMonthsAsync();

        var data = await MyLoungeAsync(ownerId, loungeId, "&from=2026-03-01T00:00:00%2B07:00&to=2026-08-31T23:59:59%2B07:00");

        var months = data.GetProperty("revenueTrend").EnumerateArray()
            .Select(m => (m.GetProperty("year").GetInt32(), m.GetProperty("month").GetInt32(),
                m.GetProperty("offlineTicketRevenue").GetDecimal()))
            .ToList();
        months.Select(m => m.Item2).Should().Equal([3, 4, 5, 6, 7, 8], "the trend spans exactly the chosen months");
        months.Single(m => m.Item2 == 3).Item3.Should().Be(200_000m);
        months.Single(m => m.Item2 == 8).Item3.Should().Be(200_000m);
    }
}
