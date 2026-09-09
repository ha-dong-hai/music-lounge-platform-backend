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
/// MLACP-317, phần nối với dữ liệu thật.
///
/// Phép đo đã được ghim chặt ở <c>RecommenderEvaluationTests</c>, không lặp lại ở đây. Việc của bộ
/// test này là chứng minh endpoint dựng đúng bài kiểm tra từ lịch sử thật, và giữ đúng hai lời hứa
/// của thiết kế: <b>không bao giờ đưa ra một con số mà thiếu baseline để so</b>, và không bao giờ
/// đưa ra con số khi dữ liệu quá mỏng.
///
/// Endpoint đo trên toàn bộ dữ liệu nền tảng nên không cô lập theo thành phố được như các bộ khác.
/// Bù lại, số trường hợp kiểm tra chỉ có thể tăng khi các test khác chạy, nên gieo đủ dữ liệu ở đây
/// là đủ để nhánh "có số" chạy tất định.
/// </summary>
[Collection("Integration")]
public sealed class RecommenderEvaluationEndpointTests
{
    private readonly ApiFactory _factory;

    public RecommenderEvaluationEndpointTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record ModelRow(
        string Model, int Cases, int Hits, decimal HitRateAtKPercent, decimal CatalogueCoveragePercent);
    private sealed record Report(
        string Status, string Method, string Caveat, int K,
        int UsersWithEnoughHistory, int CatalogueSize, IReadOnlyList<ModelRow> Models);

    /// <summary>
    /// Gieo đủ người dùng có lịch sử để phép đo vượt ngưỡng tối thiểu: mỗi người mua vé của hai buổi
    /// diễn khác nhau, và có khai một thể loại yêu thích để mô hình hợp gu có gì mà dựa vào.
    /// </summary>
    private async Task SeedListeningHistoryAsync(int people = 15, int showCount = 12)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = SeedHelper.OwnerId,
            Name = $"EvalVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Danh Gia", Ward = "P1", District = "Q1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var showIds = new List<int>();
        var tierIds = new List<int>();
        var priceIds = new List<int>();

        for (var i = 0; i < showCount; i++)
        {
            var start = DateTimeOffset.UtcNow.AddDays(20 + i);
            var show = new LoungeShow
            {
                LoungeId = lounge.Id,
                Name = $"EvalShow-{Guid.NewGuid():N}",
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(3)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();

            // Nửa số buổi gắn thể loại 1, nửa gắn thể loại 2 — để mô hình hợp gu có cơ sở phân biệt.
            db.Add(new LoungeShowGenre
            {
                LoungeShowId = show.Id,
                GenreId = i % 2 == 0 ? SeedHelper.GenreId1 : SeedHelper.GenreId2
            });

            var tier = new TicketTier
            {
                LoungeShowId = show.Id, Name = "Thuong",
                AccessType = AccessType.Physical, TotalCapacity = 500
            };
            db.Add(tier);
            await db.SaveChangesAsync();

            var price = new TicketPrice
            {
                TierId = tier.Id, Name = "Chuan", Price = 200_000m, Quota = 500, IsActive = true,
                SaleStart = DateTimeOffset.UtcNow.AddDays(-30),
                SaleEnd = start.AddHours(-2),
                PurchaseChannel = PurchaseChannel.Online
            };
            db.Add(price);
            await db.SaveChangesAsync();

            showIds.Add(show.Id);
            tierIds.Add(tier.Id);
            priceIds.Add(price.Id);
        }

        for (var p = 0; p < people; p++)
        {
            var user = new User
            {
                Email = $"eval-{Guid.NewGuid():N}@test.com",
                FullName = "Khan Gia",
                Role = UserRole.Audience,
                AuthProvider = "local",
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.Add(new UserFavouriteGenre
            {
                UserId = user.Id,
                GenreId = p % 2 == 0 ? SeedHelper.GenreId1 : SeedHelper.GenreId2
            });

            // Hai vé cho hai buổi khác nhau: một cái làm lịch sử, một cái bị giấu làm đáp án.
            for (var n = 0; n < 2; n++)
            {
                var idx = (p + n) % showIds.Count;
                db.Add(new Ticket
                {
                    // NEWSEQUENTIALID() khong co tren provider SQLite dung trong test.
                    Id = Guid.NewGuid(),
                    ShowId = showIds[idx],
                    TierId = tierIds[idx],
                    PriceId = priceIds[idx],
                    BuyerId = user.Id,
                    Status = TicketStatus.Confirmed,
                    QrCode = $"QR-{Guid.NewGuid():N}",
                    PurchaseChannel = PurchaseChannel.Online,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-10 + n)
                });
            }
            await db.SaveChangesAsync();
        }
    }

    private async Task<Report> EvaluateAsync(int k = 10)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync($"/api/v1/analytics/recommender-evaluation?k={k}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<Report>>())!.Data;
    }

    [Fact]
    public async Task ANumberIsNeverReportedWithoutABaselineToCompareItAgainst()
    {
        // Lời hứa trung tâm của thiết kế. Một điểm HR@K đứng một mình không nói lên điều gì — 0.4 là
        // tốt hay tệ? Câu trả lời chỉ có nghĩa khi đặt cạnh phép gợi ý ngây thơ nhất.
        await SeedListeningHistoryAsync();

        var report = await EvaluateAsync();

        report.Status.Should().Be("Evaluated");
        report.Models.Select(m => m.Model).Should().Contain("popularity_baseline");
        report.Models.Select(m => m.Model).Should().Contain("content_based");
    }

    [Fact]
    public async Task EveryNumberComesWithItsMethodAndItsLimits()
    {
        // Một chỉ số đánh giá đưa ra mà không kèm điều kiện áp dụng thì sẽ bị trích dẫn sai — nhất
        // là trong một báo cáo đồ án. Nên hai trường này là phần của hợp đồng, không phải trang trí.
        await SeedListeningHistoryAsync();

        var report = await EvaluateAsync();

        report.Method.Should().Contain("Leave-one-out");
        report.Caveat.Should().Contain("phổ biến",
            "phải nói rõ đánh giá offline thiên lệch theo độ phổ biến");
        report.Caveat.Should().Contain("online",
            "và nói rõ nó không thay thế được thước đo online");
    }

    [Fact]
    public async Task TheNumbersAreInternallyConsistent()
    {
        await SeedListeningHistoryAsync();

        var report = await EvaluateAsync();

        report.UsersWithEnoughHistory.Should().BeGreaterThanOrEqualTo(10);
        report.CatalogueSize.Should().BeGreaterThan(0);

        foreach (var m in report.Models)
        {
            m.Cases.Should().Be(report.UsersWithEnoughHistory,
                "mọi mô hình phải được chấm trên đúng cùng một bộ đề");
            m.Hits.Should().BeInRange(0, m.Cases);
            m.HitRateAtKPercent.Should().BeInRange(0m, 100m);
            m.CatalogueCoveragePercent.Should().BeInRange(0m, 100m);
        }
    }

    [Fact]
    public async Task LookingAtMoreSlotsNeverLowersTheScore()
    {
        // Bất biến của định nghĩa HR@K, kiểm qua đúng đường đi thật — nếu handler dựng bài kiểm tra
        // sai (ví dụ lấy mẫu lại khác nhau giữa hai lần gọi) thì bất biến này vỡ.
        await SeedListeningHistoryAsync();

        var atOne = await EvaluateAsync(k: 1);
        var atTwenty = await EvaluateAsync(k: 20);

        foreach (var model in atOne.Models.Select(m => m.Model))
        {
            var low = atOne.Models.Single(m => m.Model == model).HitRateAtKPercent;
            var high = atTwenty.Models.Single(m => m.Model == model).HitRateAtKPercent;
            high.Should().BeGreaterThanOrEqualTo(low, $"HR@K của {model} không được giảm khi K tăng");
        }
    }

    [Fact]
    public async Task TwoRunsOnTheSameDataGiveTheSameAnswer()
    {
        // Con số đưa vào báo cáo mà mỗi lần chạy lại ra một kiểu thì không ai kiểm chứng được. Đây
        // là lý do phép lấy mẫu dùng hạt giống cố định.
        await SeedListeningHistoryAsync();

        var first = await EvaluateAsync();
        var second = await EvaluateAsync();

        first.UsersWithEnoughHistory.Should().Be(second.UsersWithEnoughHistory);
        foreach (var model in first.Models.Select(m => m.Model))
        {
            first.Models.Single(m => m.Model == model).HitRateAtKPercent
                .Should().Be(second.Models.Single(m => m.Model == model).HitRateAtKPercent);
        }
    }

    [Fact]
    public async Task OnlyAdminsCanSeeIt()
    {
        // Đây là báo cáo về chất lượng nội bộ của hệ thống, không phải thông tin cho người dùng cuối.
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId)
            .GetAsync("/api/v1/analytics/recommender-evaluation");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
