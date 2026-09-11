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
/// MLACP-321. Cá nhân hoá khi chưa có gì để dựa vào — cold start, cả hai loại.
///
/// <b>Cold start của người dùng.</b> Không có bước nào bắt buộc phải hoàn thành onboarding mới dùng
/// được ứng dụng, nên phần lớn tài khoản chưa từng khai sở thích. Trước đây họ nhận đúng bảng thịnh
/// hành, mãi mãi — kể cả khi họ đã mua vé vài buổi diễn và hệ thống thừa biết họ thích gì.
///
/// <b>Cold start của buổi diễn.</b> Tập ứng viên lấy từ bảng đang được quan tâm, mà buổi vừa đăng
/// chưa có tương tác nào. Khi hoà điểm 0 thì thứ tự là "sắp diễn trước", trong khi buổi mới đăng
/// bắt buộc cách ngày diễn tối thiểu 7 ngày làm việc nên luôn nằm xa — chúng bị đẩy xuống cuối một
/// cách hệ thống. Vòng luẩn quẩn: không ai thấy nên không ai quan tâm, không ai quan tâm nên không
/// ai thấy.
/// </summary>
[Collection("Integration")]
public sealed class ColdStartPersonalisationTests
{
    private readonly ApiFactory _factory;

    public ColdStartPersonalisationTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(int Id, string Name, string? RecommendationReason);

    private async Task<(int LoungeId, string City)> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var city = $"CCity-{Guid.NewGuid():N}"[..20];
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = freshOwner.Id,
            Name = $"CVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Khoi Dau", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, city);
    }

    private async Task<int> ShowAsync(int loungeId, string name, int? genreId, double daysFromNow = 15)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(daysFromNow);
        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = name,
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        if (genreId is int g)
        {
            db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = g });
            await db.SaveChangesAsync();
        }
        return show.Id;
    }

    /// <summary>
    /// Tạo hàng loạt buổi diễn trong một lần lưu, để dựng được tập ứng viên đủ lớn mà không tốn
    /// hàng chục lần đi lại database.
    /// </summary>
    private async Task BulkShowsAsync(int loungeId, int count, int genreId, int firstDayOffset)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var shows = new List<LoungeShow>();
        for (var i = 0; i < count; i++)
        {
            var start = DateTimeOffset.UtcNow.AddDays(firstDayOffset + i);
            shows.Add(new LoungeShow
            {
                LoungeId = loungeId,
                Name = $"Cu-{i}-{Guid.NewGuid():N}"[..24],
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(3)
            });
        }
        db.LoungeShows.AddRange(shows);
        await db.SaveChangesAsync();

        db.AddRange(shows.Select(sh => new LoungeShowGenre
        {
            LoungeShowId = sh.Id, GenreId = genreId
        }));
        await db.SaveChangesAsync();
    }

    /// <summary>Tài khoản chưa từng khai sở thích và chưa bật đồng ý AI — tình trạng mặc định.</summary>
    private async Task<int> BrandNewAccountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"cold-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia Moi",
            Role = UserRole.Audience,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            AiConsent = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task BuyTicketAsync(int userId, int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tier = new TicketTier
        {
            LoungeShowId = showId, Name = "Thuong",
            AccessType = AccessType.Physical, TotalCapacity = 100
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Chuan", Price = 200_000m, Quota = 100, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-5),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(10),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();

        db.Add(new Ticket
        {
            // NEWSEQUENTIALID() khong co tren provider SQLite dung trong test.
            Id = Guid.NewGuid(),
            ShowId = showId,
            TierId = tier.Id,
            PriceId = price.Id,
            BuyerId = userId,
            Status = TicketStatus.Confirmed,
            QrCode = $"QR-{Guid.NewGuid():N}",
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<Rec>> RecommendationsAsync(int userId, string city)
    {
        var res = await _factory.CreateAuthenticatedClient(userId, "Audience")
            .GetAsync($"/api/v1/recommendations?city={city}&limit=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
    }

    // ---------- cold start của người dùng ----------

    [Fact]
    public async Task SomeoneWhoNeverFilledOnboarding_StillGetsPersonalisedResults_OnceTheyHaveBought()
    {
        // Đây là tình trạng của phần lớn tài khoản: chưa từng khai sở thích. Nhưng nếu họ đã mua vé
        // một buổi Bolero thì hệ thống ĐÃ BIẾT họ thích gì — chỉ là trước đây chưa bao giờ dùng tới,
        // và họ nhận đúng bảng thịnh hành như mọi người.
        var (loungeId, city) = await VenueAsync();
        var boughtBolero = await ShowAsync(loungeId, "Da mua - Bolero", SeedHelper.GenreId1);
        var sameKind = await ShowAsync(loungeId, "Cung gu - Bolero", SeedHelper.GenreId1, daysFromNow: 40);
        var different = await ShowAsync(loungeId, "Khac gu", SeedHelper.GenreId2, daysFromNow: 16);

        var userId = await BrandNewAccountAsync();
        await BuyTicketAsync(userId, boughtBolero);

        var recs = await RecommendationsAsync(userId, city);
        var ids = recs.Select(r => r.Id).ToList();

        ids.IndexOf(sameKind).Should().BeLessThan(ids.IndexOf(different),
            "buổi cùng thể loại với thứ họ đã mua phải lên trước, dù nó diễn muộn hơn nhiều");
    }

    [Fact]
    public async Task AndTheyAreToldWhyTheyAreSeeingIt()
    {
        // Suy gu từ giao dịch của người dùng chỉ chấp nhận được khi nó được nói ra. Không có gì
        // diễn ra sau lưng họ.
        var (loungeId, city) = await VenueAsync();
        var bought = await ShowAsync(loungeId, "Da mua", SeedHelper.GenreId1);
        var sameKind = await ShowAsync(loungeId, "Cung gu", SeedHelper.GenreId1, daysFromNow: 20);

        var userId = await BrandNewAccountAsync();
        await BuyTicketAsync(userId, bought);

        var recs = await RecommendationsAsync(userId, city);

        recs.Single(r => r.Id == sameKind).RecommendationReason
            .Should().Contain("từng quan tâm");
    }

    [Fact]
    public async Task WhatTheUserDeclaredStillWins_OverWhatWeGuessedFromHistory()
    {
        // Sở thích tự khai là lời phát biểu rõ ràng của người dùng; lịch sử mua vé chỉ là suy đoán.
        // Người khai "tôi thích thể loại 2" mà từng mua một vé thể loại 1 thì vẫn phải nhận thể loại
        // 2 — nếu không, hệ thống đang lấy suy đoán đè lên điều họ đã nói thẳng.
        var (loungeId, city) = await VenueAsync();
        var boughtGenreOne = await ShowAsync(loungeId, "Da mua - the loai 1", SeedHelper.GenreId1);
        var declaredGenreTwo = await ShowAsync(loungeId, "The loai 2", SeedHelper.GenreId2, daysFromNow: 40);
        var alsoGenreOne = await ShowAsync(loungeId, "The loai 1 khac", SeedHelper.GenreId1, daysFromNow: 41);

        var userId = await BrandNewAccountAsync();
        await BuyTicketAsync(userId, boughtGenreOne);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new UserFavouriteGenre { UserId = userId, GenreId = SeedHelper.GenreId2 });
            await db.SaveChangesAsync();
        }

        var recs = await RecommendationsAsync(userId, city);
        var ids = recs.Select(r => r.Id).ToList();

        ids.IndexOf(declaredGenreTwo).Should().BeLessThan(ids.IndexOf(alsoGenreOne));
        recs.Single(r => r.Id == declaredGenreTwo).RecommendationReason
            .Should().Contain("sở thích bạn đã chọn");
    }

    [Fact]
    public async Task AnAccountWithNothingAtAll_StillGetsSomethingUseful()
    {
        // Không khai gì, chưa mua gì. Không có cách nào cá nhân hoá, và câu trả lời đúng là bảng
        // đang thịnh hành — không phải danh sách rỗng, và cũng không phải giả vờ cá nhân hoá.
        var (loungeId, city) = await VenueAsync();
        await ShowAsync(loungeId, "Mot buoi dien", SeedHelper.GenreId1);

        var userId = await BrandNewAccountAsync();
        var recs = await RecommendationsAsync(userId, city);

        recs.Should().NotBeEmpty();
        recs.Should().OnlyContain(r => r.RecommendationReason == "Đang thịnh hành");
    }

    // ---------- cold start của buổi diễn ----------

    [Fact]
    public async Task ABrandNewShowNobodyHasSeenYet_CanStillBeRecommended()
    {
        // Vòng luẩn quẩn: buổi mới không có tương tác nên không lọt vào tập ứng viên, không lọt vào
        // tập ứng viên nên không ai thấy, không ai thấy nên mãi không có tương tác.
        //
        // Dựng đủ buổi diễn "cũ" để lấp kín tập ứng viên lấy theo mức quan tâm, rồi thêm một buổi
        // mới toanh hợp gu người dùng và diễn rất xa — tức thua ở mọi tiêu chí xếp hạng của bảng
        // thịnh hành. Nó vẫn phải được chấm điểm và lọt vào kết quả.
        var (loungeId, city) = await VenueAsync();

        // Tập ứng viên lấy theo mức quan tâm chỉ chứa 60 buổi. Dựng 61 buổi "cũ" diễn sớm hơn để
        // lấp kín đúng 60 chỗ đó, rồi thêm một buổi mới toanh diễn rất xa — nó rơi khỏi tập ứng
        // viên và nếu không có bước cứu thì không bao giờ được chấm điểm.
        await BulkShowsAsync(loungeId, 61, SeedHelper.GenreId2, firstDayOffset: 10);

        var brandNew = await ShowAsync(loungeId, "Moi toanh", SeedHelper.GenreId1, daysFromNow: 200);

        var userId = await BrandNewAccountAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new UserFavouriteGenre { UserId = userId, GenreId = SeedHelper.GenreId1 });
            await db.SaveChangesAsync();
        }

        var recs = await RecommendationsAsync(userId, city);

        recs.Select(r => r.Id).Should().Contain(brandNew,
            "buổi diễn mới hợp gu phải có cơ hội được nhìn thấy, dù chưa ai kịp quan tâm tới nó");
        recs.First().Id.Should().Be(brandNew,
            "và vì nó là buổi duy nhất hợp gu, nó phải đứng đầu");
    }

    [Fact]
    public async Task ANewShowThatDoesNotMatchTaste_DoesNotJumpTheQueue()
    {
        // Cho buổi mới một cơ hội được chấm điểm không có nghĩa là ưu ái nó. Buổi mới nhưng lệch gu
        // vẫn phải xếp dưới buổi hợp gu.
        var (loungeId, city) = await VenueAsync();
        var matching = await ShowAsync(loungeId, "Hop gu", SeedHelper.GenreId1, daysFromNow: 12);
        var newButWrong = await ShowAsync(loungeId, "Moi nhung lech gu", SeedHelper.GenreId2, daysFromNow: 90);

        var userId = await BrandNewAccountAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new UserFavouriteGenre { UserId = userId, GenreId = SeedHelper.GenreId1 });
            await db.SaveChangesAsync();
        }

        var recs = await RecommendationsAsync(userId, city);
        var ids = recs.Select(r => r.Id).ToList();

        ids.IndexOf(matching).Should().BeLessThan(ids.IndexOf(newButWrong));
    }
}
