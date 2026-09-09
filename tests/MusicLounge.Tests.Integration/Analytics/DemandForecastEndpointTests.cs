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
/// MLACP-311, phần nối với dữ liệu thật.
///
/// Phép tính đã được ghim chặt ở <c>SalesPacingForecasterTests</c>, không lặp lại ở đây. Việc của
/// bộ test này là chứng minh handler dựng đúng đầu vào cho phép tính đó — và điểm dễ sai nhất là
/// so sánh mốc thời gian: mỗi buổi diễn cũ phải được đo ở mốc "còn ngần ấy ngày nữa tới giờ diễn
/// CỦA CHÍNH NÓ", chứ không phải ở một ngày cố định trên lịch.
///
/// Không khẳng định một con số dự báo cụ thể: tập tham chiếu chung của nền tảng có cả buổi diễn do
/// các test khác tạo ra, nên con số đó không tất định. Những gì khẳng định ở đây là các bất biến
/// vẫn đúng dù nền chung có gì.
/// </summary>
[Collection("Integration")]
public sealed class DemandForecastEndpointTests
{
    private readonly ApiFactory _factory;

    public DemandForecastEndpointTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);

    private sealed record Forecast(
        int ShowId, string Status, string Explanation, int DaysUntilShow,
        int TicketsSoldSoFar, int? ProjectedFinalSales, int? ProjectedLow, int? ProjectedHigh,
        decimal? ExpectedPaceFraction, decimal VenueHistoryWeight,
        int VenueReferenceShows, int PlatformReferenceShows,
        int? Capacity, decimal? ProjectedSellThroughRate);

    private async Task<int> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = SeedHelper.OwnerId,
            Name = $"ForecastVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Dự Báo", Ward = "P1", District = "Q1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return lounge.Id;
    }

    /// <param name="ticketsCreatedAt">
    /// Thời điểm bán ra của toàn bộ vé. Đây là biến quyết định của cả bộ test: nhịp bán được đo
    /// bằng chính mốc này so với giờ diễn.
    /// </param>
    private async Task<int> ShowWithTicketsAsync(
        int loungeId, LoungeShowStatus status, DateTimeOffset scheduledStart,
        int ticketCount, DateTimeOffset ticketsCreatedAt, int? capacity = 200)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = $"ForecastShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = status,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Thường",
            AccessType = AccessType.Physical, TotalCapacity = capacity
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Chuẩn", Price = 200_000m, Quota = capacity, IsActive = true,
            SaleStart = scheduledStart.AddDays(-90),
            SaleEnd = scheduledStart.AddHours(-2),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();

        for (var i = 0; i < ticketCount; i++)
        {
            db.Add(new Ticket
            {
                // NEWSEQUENTIALID() không có trên provider SQLite dùng trong test.
                Id = Guid.NewGuid(),
                ShowId = show.Id,
                TierId = tier.Id,
                PriceId = price.Id,
                BuyerId = SeedHelper.AudienceId,
                Status = TicketStatus.Confirmed,
                QrCode = $"QR-{Guid.NewGuid():N}",
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = ticketsCreatedAt
            });
        }
        await db.SaveChangesAsync();

        return show.Id;
    }

    private async Task<Forecast> ForecastAsync(int showId, int loungeId)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", loungeId)
            .GetAsync($"/api/v1/analytics/shows/{showId}/demand-forecast");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<Forecast>>())!.Data;
    }

    /// <summary>
    /// Ba buổi diễn đã xong, vé của chúng đều bán xong từ rất sớm — 60 ngày trước giờ diễn của
    /// chính chúng. Với buổi đang xét còn 10 ngày, nhịp bán riêng của phòng trà này bằng 1.0:
    /// "tới mốc này thì lịch sử cho thấy đã bán hết".
    /// </summary>
    private async Task<(int TargetShowId, int LoungeId)> SeedVenueWithFastSellingHistoryAsync(
        int targetTicketCount)
    {
        var loungeId = await VenueAsync();

        foreach (var daysAgo in new[] { 30, 40, 50 })
        {
            var start = DateTimeOffset.UtcNow.AddDays(-daysAgo);
            await ShowWithTicketsAsync(
                loungeId, LoungeShowStatus.Ended, start,
                ticketCount: 50, ticketsCreatedAt: start.AddDays(-60));
        }

        var targetStart = DateTimeOffset.UtcNow.AddDays(10);
        var targetShowId = await ShowWithTicketsAsync(
            loungeId, LoungeShowStatus.Published, targetStart,
            ticketCount: targetTicketCount, ticketsCreatedAt: DateTimeOffset.UtcNow.AddDays(-5));

        return (targetShowId, loungeId);
    }

    // ---------- điểm dễ sai nhất: so cùng một mốc ----------

    [Fact]
    public async Task EachPastShowIsMeasuredAtItsOwnLeadTime_NotAtAFixedCalendarDate()
    {
        // Ba buổi diễn cũ đều bán hết vé từ 60 ngày trước ngày diễn của CHÍNH NÓ. Buổi đang xét
        // còn 10 ngày, nên nhịp riêng của phòng trà = 1.0, và dự báo phải sát với số vé đã bán.
        //
        // Nếu handler đo nhầm — ví dụ đếm vé bán trước một ngày cố định trên lịch thay vì trước
        // mốc riêng của từng buổi diễn — nhịp sẽ ra gần 0, và dự báo bị thổi lên nhiều lần.
        var (showId, loungeId) = await SeedVenueWithFastSellingHistoryAsync(targetTicketCount: 20);

        var f = await ForecastAsync(showId, loungeId);

        f.Status.Should().Be("Forecast");
        f.VenueReferenceShows.Should().Be(3);
        f.TicketsSoldSoFar.Should().Be(20);
        f.ProjectedFinalSales.Should().BeInRange(20, 40,
            "nhịp riêng bằng 1.0 và trọng số 50% nên nhịp dùng để tính không thể dưới 0.5, " +
            "tức dự báo không thể quá gấp đôi số vé đã bán");
    }

    [Fact]
    public async Task AVenueAtTheMinimum_SplitsItsWeightWithThePlatform()
    {
        // 3 buổi diễn riêng → trọng số 3/(3+3). Ghim lại vì đây là chỗ phép hiệu chỉnh mẫu nhỏ
        // thật sự tác động lên con số Owner nhìn thấy.
        var (showId, loungeId) = await SeedVenueWithFastSellingHistoryAsync(targetTicketCount: 20);

        var f = await ForecastAsync(showId, loungeId);

        f.VenueHistoryWeight.Should().Be(0.5m);
        f.PlatformReferenceShows.Should().BeGreaterThanOrEqualTo(3);
    }

    // ---------- các bất biến ----------

    [Fact]
    public async Task TheForecastNeverFallsBelowWhatIsAlreadySold()
    {
        var (showId, loungeId) = await SeedVenueWithFastSellingHistoryAsync(targetTicketCount: 20);

        var f = await ForecastAsync(showId, loungeId);

        f.ProjectedFinalSales.Should().BeGreaterThanOrEqualTo(f.TicketsSoldSoFar);
        f.ProjectedLow.Should().BeGreaterThanOrEqualTo(f.TicketsSoldSoFar);
    }

    [Fact]
    public async Task TheForecastNeverExceedsTheRoom()
    {
        var (showId, loungeId) = await SeedVenueWithFastSellingHistoryAsync(targetTicketCount: 20);

        var f = await ForecastAsync(showId, loungeId);

        f.Capacity.Should().Be(200);
        f.ProjectedHigh.Should().BeLessThanOrEqualTo(200);
        f.ProjectedSellThroughRate.Should().BeLessThanOrEqualTo(1m);
    }

    [Fact]
    public async Task EveryAnswerExplainsWhatItIsBasedOn()
    {
        // Một dự báo không nói được nó dựa trên cái gì thì không dùng để quyết định gì. Đây là
        // trường bắt buộc, không phải trang trí.
        var (showId, loungeId) = await SeedVenueWithFastSellingHistoryAsync(targetTicketCount: 20);

        var f = await ForecastAsync(showId, loungeId);

        f.Explanation.Should().NotBeNullOrWhiteSpace();
        f.Explanation.Should().Contain("3", "phải nói ra đang dựa trên mấy buổi diễn");
        f.ExpectedPaceFraction.Should().BeGreaterThan(0m,
            "tỉ lệ dùng để suy ra dự báo phải đưa ra được để kiểm chứng");
    }

    // ---------- các trạng thái không phải dự báo ----------

    [Fact]
    public async Task AShowThatHasAlreadyStarted_ReportsRealSalesAndSaysItIsNotAForecast()
    {
        var loungeId = await VenueAsync();
        var showId = await ShowWithTicketsAsync(
            loungeId, LoungeShowStatus.Ongoing, DateTimeOffset.UtcNow.AddHours(-1),
            ticketCount: 12, ticketsCreatedAt: DateTimeOffset.UtcNow.AddDays(-3));

        var f = await ForecastAsync(showId, loungeId);

        f.TicketsSoldSoFar.Should().Be(12);
        f.ProjectedFinalSales.Should().Be(12);
        f.Explanation.Should().Contain("không phải dự báo");
    }

    [Fact]
    public async Task AnotherOwnerCannotSeeIt()
    {
        // Nhịp bán và dự báo doanh thu là thông tin kinh doanh của phòng trà đó.
        var (showId, _) = await SeedVenueWithFastSellingHistoryAsync(targetTicketCount: 20);

        var res = await _factory
            .CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner", SeedHelper.OtherLoungeId)
            .GetAsync($"/api/v1/analytics/shows/{showId}/demand-forecast");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
