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
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-316. Cá nhân hoá cho từng tài khoản và cho khách chưa đăng nhập.
///
/// Trước đây endpoint gợi ý bắt buộc đăng nhập, nên khách vãng lai nhận 401 — mà đó chính là người
/// cần được thuyết phục nhất. Và người đã đăng nhập nhưng chưa bật đồng ý AI cũng chỉ nhận đúng
/// bảng thịnh hành chung, kể cả khi họ vừa tự tay chọn thể loại mình thích ở bước onboarding.
///
/// Mỗi test dựng một thành phố riêng và lọc theo thành phố đó, nên dữ liệu của các test khác không
/// lọt vào tập ứng viên.
/// </summary>
[Collection("Integration")]
public sealed class PersonalisedFeedTests
{
    private readonly ApiFactory _factory;

    public PersonalisedFeedTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(int Id, string Name, float RecommendationScore, string? RecommendationReason);

    private async Task<(int LoungeId, string City)> VenueInItsOwnCityAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var city = $"PCity-{Guid.NewGuid():N}"[..20];
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = freshOwner.Id,
            Name = $"PVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Ca Nhan", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, city);
    }

    private async Task<int> ShowWithGenreAsync(int loungeId, string name, int? genreId, double daysFromNow = 10)
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

    /// <summary>Một tài khoản đã khai sở thích ở bước onboarding nhưng KHÔNG bật đồng ý AI.</summary>
    private async Task<int> UserWhoDeclaredTasteButDidNotConsentAsync(int favouriteGenreId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"declared-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia Da Khai Gu",
            Role = UserRole.Audience,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            AiConsent = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Add(new UserFavouriteGenre { UserId = user.Id, GenreId = favouriteGenreId });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<IReadOnlyList<Rec>> AskAsync(HttpClient client, string query)
    {
        var res = await client.GetAsync($"/api/v1/recommendations?{query}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
    }

    // ---------- khách chưa đăng nhập ----------

    [Fact]
    public async Task AGuestGetsRecommendations_InsteadOfBeingTurnedAway()
    {
        // Trước đây endpoint này trả 401 cho khách vãng lai. Người chưa có tài khoản là người cần
        // được thuyết phục nhất, và câu trả lời dành cho họ là một mã lỗi.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        await ShowWithGenreAsync(loungeId, "Bat ky", SeedHelper.GenreId1);

        var res = await _factory.CreateClient().GetAsync($"/api/v1/recommendations?city={city}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AGuestWhoJustBrowsedSomething_GetsMoreOfTheSameKind()
    {
        // "Vì bạn vừa xem" — cách cá nhân hoá cho người mình không biết là ai. Ngữ cảnh đến trong
        // request, dùng để sắp xếp câu trả lời, rồi thôi.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var browsed = await ShowWithGenreAsync(loungeId, "Buoi da xem", SeedHelper.GenreId1);
        var sameKind = await ShowWithGenreAsync(loungeId, "Cung the loai", SeedHelper.GenreId1);
        var different = await ShowWithGenreAsync(loungeId, "The loai khac", SeedHelper.GenreId2);

        var recs = await AskAsync(_factory.CreateClient(),
            $"city={city}&recentShowIds={browsed}&limit=50");

        var ids = recs.Select(r => r.Id).ToList();
        ids.Should().Contain(sameKind);
        ids.IndexOf(sameKind).Should().BeLessThan(ids.IndexOf(different),
            "buổi diễn cùng thể loại với thứ khách vừa xem phải được đẩy lên trên");
        recs.Single(r => r.Id == sameKind).RecommendationReason.Should().Contain("vừa xem",
            "khách phải hiểu vì sao mình được gợi ý cái này");
    }

    [Fact]
    public async Task AGuestFilteringByGenre_GetsThatGenreFirst()
    {
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var wanted = await ShowWithGenreAsync(loungeId, "The loai dang tim", SeedHelper.GenreId1);
        var other = await ShowWithGenreAsync(loungeId, "The loai khac", SeedHelper.GenreId2);

        var recs = await AskAsync(_factory.CreateClient(),
            $"city={city}&genreIds={SeedHelper.GenreId1}&limit=50");

        var ids = recs.Select(r => r.Id).ToList();
        ids.IndexOf(wanted).Should().BeLessThan(ids.IndexOf(other));
    }

    [Fact]
    public async Task AGuestWeKnowNothingAbout_GetsTrendingAndIsToldSo()
    {
        // Không được giả vờ cá nhân hoá khi không có gì để cá nhân hoá. Xếp theo một cái gu rỗng
        // chỉ tạo ra thứ tự ngẫu nhiên đội lốt gợi ý riêng.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        await ShowWithGenreAsync(loungeId, "Mot buoi dien", SeedHelper.GenreId1);

        var recs = await AskAsync(_factory.CreateClient(), $"city={city}&limit=50");

        recs.Should().NotBeEmpty();
        recs.Should().OnlyContain(r => r.RecommendationReason == "Đang thịnh hành" && r.RecommendationScore == 0f);
    }

    [Fact]
    public async Task NothingAboutAGuestIsWrittenDown()
    {
        // Lời hứa về quyền riêng tư phải được kiểm, không phải chỉ được ghi trong chú thích. Một
        // request của khách vãng lai không được để lại hồ sơ gợi ý hay bản ghi hành vi nào.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var browsed = await ShowWithGenreAsync(loungeId, "Buoi da xem", SeedHelper.GenreId1);

        int recsBefore, logsBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            recsBefore = await db.Set<AiRecommendation>().CountAsync();
            logsBefore = await db.Set<UserBehaviourLog>().CountAsync();
        }

        await AskAsync(_factory.CreateClient(), $"city={city}&recentShowIds={browsed}&limit=50");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<AiRecommendation>().CountAsync()).Should().Be(recsBefore);
            (await db.Set<UserBehaviourLog>().CountAsync()).Should().Be(logsBefore);
        }
    }

    // ---------- tài khoản đã đăng nhập ----------

    [Fact]
    public async Task ADeclaredTasteIsHonoured_EvenWithoutTurningOnAiConsent()
    {
        // Đây là điểm sửa quan trọng nhất. Người dùng đi qua onboarding, tự tay chọn thể loại mình
        // thích — rồi nhận đúng danh sách chung như mọi người, chỉ vì không tích ô đồng ý AI.
        //
        // Đồng ý AI là để chặn việc hệ thống SUY ĐOÁN ra con người bạn từ hành vi. Tôn trọng một sở
        // thích bạn TỰ KHAI thì không cần xin phép — đó là làm đúng việc bạn vừa yêu cầu.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var matching = await ShowWithGenreAsync(loungeId, "Dung gu", SeedHelper.GenreId1);
        var unrelated = await ShowWithGenreAsync(loungeId, "Khong lien quan", SeedHelper.GenreId2);

        var userId = await UserWhoDeclaredTasteButDidNotConsentAsync(SeedHelper.GenreId1);
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");

        var recs = await AskAsync(client, $"city={city}&limit=50");

        var ids = recs.Select(r => r.Id).ToList();
        ids.IndexOf(matching).Should().BeLessThan(ids.IndexOf(unrelated));
        recs.Single(r => r.Id == matching).RecommendationReason.Should().Contain("sở thích");
    }

    [Fact]
    public async Task AVenueTheListenerFollows_GetsAnExtraNudge()
    {
        // Bấm theo dõi một phòng trà là hành động dứt khoát, và nó đáng được tính ngoài việc khớp
        // thể loại — hai buổi diễn ngang gu thì buổi ở phòng trà bạn theo dõi lên trước.
        var (followedLounge, city) = await VenueInItsOwnCityAsync();
        var (otherLounge, _) = await VenueInItsOwnCityAsync();

        // Đưa phòng trà thứ hai về cùng thành phố để cả hai cùng nằm trong tập ứng viên.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lounge = await db.Set<MusicLoungeVenue>().SingleAsync(l => l.Id == otherLounge);
            lounge.Address.City = city;
            await db.SaveChangesAsync();
        }

        var atFollowed = await ShowWithGenreAsync(followedLounge, "O venue dang theo doi", SeedHelper.GenreId1);
        var atOther = await ShowWithGenreAsync(otherLounge, "O venue khac", SeedHelper.GenreId1, daysFromNow: 9);

        var userId = await UserWhoDeclaredTasteButDidNotConsentAsync(SeedHelper.GenreId1);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new Follow { UserId = userId, LoungeId = followedLounge });
            await db.SaveChangesAsync();
        }

        var recs = await AskAsync(_factory.CreateAuthenticatedClient(userId, "Audience"), $"city={city}&limit=50");

        var ids = recs.Select(r => r.Id).ToList();
        ids.IndexOf(atFollowed).Should().BeLessThan(ids.IndexOf(atOther),
            "hai buổi cùng thể loại, buổi ở phòng trà đang theo dõi phải lên trước dù diễn muộn hơn");
    }

    [Fact]
    public async Task PersonalisationReordersTheList_ItDoesNotShrinkIt()
    {
        // Cá nhân hoá là sắp xếp lại, không phải cắt bớt lựa chọn. Buổi diễn không khớp gu vẫn phải
        // còn trong danh sách, chỉ nằm dưới — nếu không, người dùng bị nhốt trong đúng cái gu họ
        // từng khai và không bao giờ thấy thứ gì khác.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var matching = await ShowWithGenreAsync(loungeId, "Dung gu", SeedHelper.GenreId1);
        var unrelated = await ShowWithGenreAsync(loungeId, "Khong lien quan", SeedHelper.GenreId2);
        var untagged = await ShowWithGenreAsync(loungeId, "Chua gan the loai", null);

        var userId = await UserWhoDeclaredTasteButDidNotConsentAsync(SeedHelper.GenreId1);

        var recs = await AskAsync(_factory.CreateAuthenticatedClient(userId, "Audience"), $"city={city}&limit=50");

        recs.Select(r => r.Id).Should().Contain([matching, unrelated, untagged]);
    }

    [Fact]
    public async Task AnAccountThatNeverDeclaredAnything_StillGetsTrending()
    {
        // Tài khoản mới tinh, chưa khai gì: không có gì để cá nhân hoá, và câu trả lời đúng là bảng
        // đang thịnh hành chứ không phải danh sách rỗng.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        await ShowWithGenreAsync(loungeId, "Mot buoi dien", SeedHelper.GenreId1);

        var recs = await AskAsync(
            _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience"), $"city={city}&limit=50");

        recs.Should().NotBeEmpty();
        recs.Should().OnlyContain(r => r.RecommendationReason == "Đang thịnh hành");
    }
}
