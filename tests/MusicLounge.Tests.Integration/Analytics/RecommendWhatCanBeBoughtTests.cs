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
/// MLACP-327. Gợi ý phải là thứ người dùng còn làm được gì đó với nó.
///
/// Danh sách duyệt có hẳn tham số <c>includeSoldOut</c> và lọc buổi hết vé. Danh sách gợi ý thì
/// không có gì tương đương — nó chỉ lọc theo trạng thái buổi diễn và giờ kết thúc. Nên hệ thống
/// chủ động đặt trước mặt người dùng những buổi họ không thể mua vé, vì ba lý do khác hẳn nhau:
///
/// <list type="number">
/// <item><b>Hết vé</b> — mọi mức giá đều bán hết quota (tính cả vé đang giữ chỗ).</item>
/// <item><b>Đã đóng bán</b> — quá mốc <c>SaleEnd</c>, hoặc quá giờ nhận khách cuối theo BR-31, tức
/// buổi diễn sắp tan.</item>
/// <item><b>Chưa mở bán</b> — <c>now</c> còn trước <c>SaleStart</c>.</item>
/// </list>
///
/// <b>Ba tình huống này không giống nhau nên không được xử như nhau.</b> Hết vé và đã đóng bán thì
/// người dùng không còn làm gì được nữa — đẩy xuống cuối. Nhưng "chưa mở bán" là thứ người hâm mộ
/// muốn biết TRƯỚC để còn canh; đẩy nó xuống là đi ngược đúng lợi ích của họ.
///
/// Và vẫn giữ nguyên tắc từ MLACP-319/320: <b>chỉ sắp xếp lại, không làm danh sách ngắn đi.</b> Kho
/// hiện quá nhỏ, cắt cứng sẽ ra màn hình trắng — mà màn hình trắng còn tệ hơn một danh sách có thứ
/// hơi thừa.
/// </summary>
[Collection("Integration")]
public sealed class RecommendWhatCanBeBoughtTests
{
    private readonly ApiFactory _factory;

    public RecommendWhatCanBeBoughtTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(int Id, string Name);

    private async Task<(int LoungeId, string City)> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var city = $"BCity-{Guid.NewGuid():N}"[..20];
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = freshOwner.Id,
            Name = $"BVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Con Ve", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, city);
    }

    private async Task<int> ShowAsync(int loungeId, string name, double daysFromNow)
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

        db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = SeedHelper.GenreId1 });
        await db.SaveChangesAsync();
        return show.Id;
    }

    /// <param name="quota">Số vé mở bán. Đặt bằng số vé đã bán để dựng tình huống hết vé.</param>
    /// <param name="sold">Số vé đã ở trạng thái Confirmed.</param>
    private async Task PriceAsync(
        int showId, int quota, int sold = 0,
        double saleStartDaysFromNow = -7, double? saleEndDaysFromNow = 30)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tier = new TicketTier
        {
            LoungeShowId = showId, Name = "Thuong",
            AccessType = AccessType.Physical, TotalCapacity = 200
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Chuan", Price = 200_000m, Quota = quota, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(saleStartDaysFromNow),
            SaleEnd = saleEndDaysFromNow is { } d ? DateTimeOffset.UtcNow.AddDays(d) : null,
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();

        for (var i = 0; i < sold; i++)
        {
            db.Add(new Ticket
            {
                // NEWSEQUENTIALID() khong co tren provider SQLite dung trong test.
                Id = Guid.NewGuid(),
                ShowId = showId, TierId = tier.Id, PriceId = price.Id,
                BuyerId = SeedHelper.AudienceId,
                Status = TicketStatus.Confirmed,
                QrCode = $"QR-{Guid.NewGuid():N}",
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task<int> ListenerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"buy-{Guid.NewGuid():N}@test.com",
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

    private async Task<IReadOnlyList<Rec>> RecommendationsAsync(int userId, string city)
    {
        var res = await _factory.CreateAuthenticatedClient(userId, "Audience")
            .GetAsync($"/api/v1/recommendations?city={city}&limit=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
    }

    // ---------- không mua được nữa thì xuống cuối ----------

    [Fact]
    public async Task ASoldOutShowDropsBelowOneYouCanStillBuy()
    {
        // Buổi hết vé được đặt diễn SỚM HƠN, nên nếu hai bên hoà điểm hợp gu thì nó thắng ở mốc phá
        // hoà và lên đứng trên. Không dựng đúng thế thì bài kiểm tra không kiểm được gì.
        var (loungeId, city) = await VenueAsync();
        var soldOut = await ShowAsync(loungeId, "Het ve", daysFromNow: 10);
        var available = await ShowAsync(loungeId, "Con ve", daysFromNow: 20);

        await PriceAsync(soldOut, quota: 2, sold: 2);
        await PriceAsync(available, quota: 100, sold: 0);

        var ids = (await RecommendationsAsync(await ListenerAsync(), city)).Select(r => r.Id).ToList();

        ids.IndexOf(available).Should().BeLessThan(ids.IndexOf(soldOut),
            "mời người ta mua một thứ đã hết vé là lãng phí một suất khám phá");
    }

    [Fact]
    public async Task AShowWhoseSaleHasClosedDropsBelow()
    {
        var (loungeId, city) = await VenueAsync();
        var closed = await ShowAsync(loungeId, "Da dong ban", daysFromNow: 10);
        var available = await ShowAsync(loungeId, "Con ban", daysFromNow: 20);

        // Đợt bán đã kết thúc từ hôm qua, dù buổi diễn còn mười ngày nữa.
        await PriceAsync(closed, quota: 100, saleEndDaysFromNow: -1);
        await PriceAsync(available, quota: 100);

        var ids = (await RecommendationsAsync(await ListenerAsync(), city)).Select(r => r.Id).ToList();

        ids.IndexOf(available).Should().BeLessThan(ids.IndexOf(closed));
    }

    [Fact]
    public async Task AShowPastTheLastEntryCutoffDropsBelow()
    {
        // BR-31 (MLACP-309): trần cứng là giờ nhận khách cuối, mặc định 60 phút trước khi buổi diễn
        // kết thúc — bán vé nguyên giá lúc chương trình sắp tan là bán một thứ không còn gì để xem.
        // Buổi này đang diễn và chỉ còn 20 phút nữa là hết, nên đã quá mốc đó.
        var (loungeId, city) = await VenueAsync();
        var almostOver = await ShowAsync(loungeId, "Sap tan", daysFromNow: -3.0 / 24 + 20.0 / (24 * 60));
        var available = await ShowAsync(loungeId, "Con ban", daysFromNow: 20);

        await PriceAsync(almostOver, quota: 100);
        await PriceAsync(available, quota: 100);

        var ids = (await RecommendationsAsync(await ListenerAsync(), city)).Select(r => r.Id).ToList();

        ids.IndexOf(available).Should().BeLessThan(ids.IndexOf(almostOver),
            "quá giờ nhận khách cuối thì vé không bán được nữa");
    }

    // ---------- chưa mở bán thì KHÔNG bị đẩy xuống ----------

    [Fact]
    public async Task AShowWhoseSaleHasNotOpenedYetKeepsItsPlace()
    {
        // Đây là nửa dễ làm sai nhất. "Chưa mở bán" cũng là không mua được ngay, nhưng nó là thứ
        // người hâm mộ muốn biết trước để còn canh mua. Gộp chung với "hết vé" là đẩy đúng thứ họ
        // cần nhất xuống đáy.
        var (loungeId, city) = await VenueAsync();
        var notOpenYet = await ShowAsync(loungeId, "Sap mo ban", daysFromNow: 10);
        var available = await ShowAsync(loungeId, "Dang ban", daysFromNow: 20);

        await PriceAsync(notOpenYet, quota: 100, saleStartDaysFromNow: 3);
        await PriceAsync(available, quota: 100);

        var ids = (await RecommendationsAsync(await ListenerAsync(), city)).Select(r => r.Id).ToList();

        ids.IndexOf(notOpenYet).Should().BeLessThan(ids.IndexOf(available),
            "buổi sắp mở bán phải giữ nguyên vị trí, nó diễn sớm hơn nên đứng trên");
    }

    [Fact]
    public async Task AShowWithNoTicketingConfiguredKeepsItsPlace()
    {
        // Chốt giữ quan trọng: buổi vừa đăng thường chưa kịp cấu hình hạng vé. Nếu coi "không có
        // mức giá nào" là "không mua được" thì mọi buổi mới đều bị đẩy xuống đáy — tức là xoá sạch
        // phần cold start của buổi diễn vừa làm ở MLACP-321. Chỉ đẩy xuống khi CÓ BẰNG CHỨNG là
        // không mua được, không đẩy vì thiếu thông tin.
        var (loungeId, city) = await VenueAsync();
        var noTicketing = await ShowAsync(loungeId, "Chua cau hinh ve", daysFromNow: 10);
        var available = await ShowAsync(loungeId, "Dang ban", daysFromNow: 20);

        await PriceAsync(available, quota: 100);

        var ids = (await RecommendationsAsync(await ListenerAsync(), city)).Select(r => r.Id).ToList();

        ids.IndexOf(noTicketing).Should().BeLessThan(ids.IndexOf(available),
            "chưa cấu hình vé là thiếu thông tin, không phải bằng chứng không mua được");
    }

    // ---------- không bao giờ làm danh sách ngắn đi ----------

    [Fact]
    public async Task WhenEverythingIsSoldOutTheListIsStillNotEmpty()
    {
        // Cùng nguyên tắc với MLACP-319: một màn hình trống còn tệ hơn một danh sách có thứ hơi
        // thừa. Người dùng ít nhất còn biết nền tảng có gì và buổi nào đang hút khách.
        var (loungeId, city) = await VenueAsync();
        var only = await ShowAsync(loungeId, "Buoi duy nhat, het ve", daysFromNow: 10);
        await PriceAsync(only, quota: 1, sold: 1);

        var recs = await RecommendationsAsync(await ListenerAsync(), city);

        recs.Should().NotBeEmpty("thà hiện một buổi đã hết vé còn hơn hiện màn hình trắng");
        recs.Select(r => r.Id).Should().Contain(only);
    }
}
