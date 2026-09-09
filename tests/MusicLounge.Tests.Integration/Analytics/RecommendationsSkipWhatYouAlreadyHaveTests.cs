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
/// MLACP-319. Gợi ý không được giới thiệu lại thứ người dùng đã có.
///
/// Trước đây không đường nào loại trừ: người mua vé "Đêm nhạc Trịnh" xong vẫn tiếp tục được gợi ý
/// đúng buổi đó. Gợi ý sinh ra để giúp KHÁM PHÁ, nên giới thiệu lại thứ họ đã mua là điều ngược hẳn
/// với mục đích — và tệ hơn, nó chiếm mất chỗ của thứ họ chưa tìm thấy.
///
/// Nhưng cắt hẳn cũng sai khi kho nhỏ: nền tảng này hiện chỉ có vài buổi diễn đang mở bán, nên cắt
/// hẳn sẽ trả về danh sách rỗng. Một danh sách rỗng còn tệ hơn một danh sách có thứ hơi thừa. Nên
/// quy tắc là ưu tiên thứ chưa thấy, chỉ bù bằng thứ đã có khi không còn gì khác để hiện.
/// </summary>
[Collection("Integration")]
public sealed class RecommendationsSkipWhatYouAlreadyHaveTests
{
    private readonly ApiFactory _factory;

    public RecommendationsSkipWhatYouAlreadyHaveTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(int Id, string Name);

    private async Task<(int LoungeId, string City)> VenueInItsOwnCityAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var city = $"SCity-{Guid.NewGuid():N}"[..20];
        var lounge = new MusicLoungeVenue
        {
            OwnerId = SeedHelper.OwnerId,
            Name = $"SVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Da Co", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, city);
    }

    private async Task<int> ShowAsync(int loungeId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(15);
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

        // Gắn thể loại để người dùng có gu khớp được với nó.
        db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = SeedHelper.GenreId1 });
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<int> ListenerWhoLikesGenreOneAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"owns-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia",
            Role = UserRole.Audience,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            AiConsent = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Add(new UserFavouriteGenre { UserId = user.Id, GenreId = SeedHelper.GenreId1 });
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

    private async Task SaveToWishlistAsync(int userId, int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(new ShowWishlist
        {
            UserId = userId, LoungeShowId = showId, CreatedAt = DateTimeOffset.UtcNow
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

    [Fact]
    public async Task AShowYouAlreadyBoughtATicketFor_DropsBelowTheOnesYouHaveNotSeen()
    {
        // Điều cơ bản nhất: gợi ý là để khám phá, không phải để nhắc lại thứ đã mua.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var bought = await ShowAsync(loungeId, "Da mua ve");
        var notSeenYet = await ShowAsync(loungeId, "Chua tung thay");

        var userId = await ListenerWhoLikesGenreOneAsync();
        await BuyTicketAsync(userId, bought);

        var ids = (await RecommendationsAsync(userId, city)).Select(r => r.Id).ToList();

        ids.IndexOf(notSeenYet).Should().BeLessThan(ids.IndexOf(bought),
            "buổi chưa từng thấy phải đứng trên buổi đã mua vé");
    }

    [Fact]
    public async Task AShowYouAlreadySaved_AlsoDropsBelow()
    {
        // Buổi đã lưu quan tâm thì người dùng tự tìm ra rồi, và đã có màn hình riêng cho danh sách
        // đó. Để nó chiếm chỗ trong gợi ý là lãng phí một suất khám phá.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var saved = await ShowAsync(loungeId, "Da luu quan tam");
        var notSeenYet = await ShowAsync(loungeId, "Chua tung thay");

        var userId = await ListenerWhoLikesGenreOneAsync();
        await SaveToWishlistAsync(userId, saved);

        var ids = (await RecommendationsAsync(userId, city)).Select(r => r.Id).ToList();

        ids.IndexOf(notSeenYet).Should().BeLessThan(ids.IndexOf(saved));
    }

    [Fact]
    public async Task WhenEverythingIsAlreadyOwned_TheListIsStillNotEmpty()
    {
        // Kho nhỏ là tình trạng thật của nền tảng này. Cắt hẳn thứ đã có sẽ trả về danh sách rỗng,
        // mà một màn hình gợi ý trống còn tệ hơn một danh sách có thứ hơi thừa.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var only = await ShowAsync(loungeId, "Buoi dien duy nhat");

        var userId = await ListenerWhoLikesGenreOneAsync();
        await BuyTicketAsync(userId, only);

        var recs = await RecommendationsAsync(userId, city);

        recs.Should().NotBeEmpty("thà hiện lại thứ đã có còn hơn hiện một màn hình trống");
        recs.Select(r => r.Id).Should().Contain(only);
    }

    [Fact]
    public async Task BuyingATicketRemovesItImmediately_WithoutWaitingForTheCacheToExpire()
    {
        // Loại trừ được làm lúc TRẢ KẾT QUẢ chứ không lúc tính sẵn. Nếu làm lúc tính sẵn, người vừa
        // mua vé sẽ còn thấy buổi đó trong gợi ý suốt tới 6 tiếng.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var target = await ShowAsync(loungeId, "Sap mua");
        var other = await ShowAsync(loungeId, "Buoi khac");

        var userId = await ListenerWhoLikesGenreOneAsync();

        var before = (await RecommendationsAsync(userId, city)).Select(r => r.Id).ToList();
        before.Should().Contain(target, "tiền đề: trước khi mua thì nó vẫn được gợi ý bình thường");

        await BuyTicketAsync(userId, target);

        var after = (await RecommendationsAsync(userId, city)).Select(r => r.Id).ToList();
        after.IndexOf(other).Should().BeLessThan(after.IndexOf(target),
            "vừa mua xong là nó phải tụt xuống ngay, không đợi cache hết hạn");
    }

    [Fact]
    public async Task AGuestIsNotAffected_BecauseTheSystemDoesNotKnowWhoTheyAre()
    {
        // Khách vãng lai không có gì để loại trừ, và hệ thống không được đi tìm hiểu xem họ đã mua
        // gì. Đây là kiểm chứng rằng đường của khách không vô tình đụng vào dữ liệu người dùng.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var show = await ShowAsync(loungeId, "Mot buoi dien");

        var res = await _factory.CreateClient().GetAsync($"/api/v1/recommendations?city={city}&limit=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var recs = (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
        recs.Select(r => r.Id).Should().Contain(show);
    }
}
