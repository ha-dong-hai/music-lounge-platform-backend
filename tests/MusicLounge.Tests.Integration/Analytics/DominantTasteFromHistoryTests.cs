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
/// MLACP-323. Gu suy từ lịch sử phải là gu CHÍNH, không phải mọi thứ từng chạm vào.
///
/// MLACP-321 suy gu người dùng từ thẻ phân loại của những buổi họ đã mua vé hoặc đã lưu quan tâm.
/// Phép suy đó là phép hợp thuần tuý: mọi thẻ đều vào, đếm ngang nhau, không giới hạn thời gian.
/// Hai hệ quả, và cả hai đều tệ dần theo đúng mức độ người dùng dùng sản phẩm:
///
/// <b>Một lần lệch gu nằm lại vĩnh viễn.</b> Mua vé hộ bạn một buổi EDM, hoặc thử một thể loại lạ
/// đúng một lần, là thể loại đó vào hồ sơ gu ngang hàng với thứ họ mua mười hai lần. Không có cách
/// nào rút lại — kể cả khi họ không bao giờ đụng tới nó nữa.
///
/// <b>Tập gu chỉ phình ra.</b> Càng dùng lâu nó càng phủ gần hết thể loại, mẫu số Jaccard càng lớn,
/// mọi buổi diễn chấm điểm gần bằng nhau. Cá nhân hoá âm thầm phẳng trở lại thành bảng thịnh hành,
/// trong khi lý do hiện cho người dùng vẫn nói "Giống những buổi diễn bạn từng quan tâm". Đó là một
/// lời hứa không còn đúng.
///
/// Quy tắc thay thế đọc được thành một câu: <b>một thẻ phải chiếm ít nhất một nửa số buổi so với
/// thẻ mạnh nhất mới được coi là gu — dưới mức đó nó là một lần thử, không phải một sở thích.</b>
///
/// Chỉ áp cho phần SUY RA. Sở thích tự khai không đụng tới, và công thức chấm điểm trong tài liệu
/// cũng không đụng tới — chỉ đổi cách dựng tập đầu vào cho nó.
/// </summary>
[Collection("Integration")]
public sealed class DominantTasteFromHistoryTests
{
    private readonly ApiFactory _factory;

    public DominantTasteFromHistoryTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(int Id, string Name);

    private async Task<(int LoungeId, string City)> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var city = $"GCity-{Guid.NewGuid():N}"[..20];
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = freshOwner.Id,
            Name = $"GVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Gu Chinh", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, city);
    }

    private async Task<int> ShowAsync(int loungeId, string name, int genreId, double daysFromNow)
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

        db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = genreId });
        await db.SaveChangesAsync();
        return show.Id;
    }

    /// <summary>Tài khoản chưa từng khai sở thích — điều kiện để đường suy gu từ lịch sử chạy.</summary>
    private async Task<int> AccountWithNoDeclaredTasteAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"gu-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia",
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

    private async Task SaveToWishlistAsync(int userId, int showId, int minutesAgo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(new ShowWishlist
        {
            UserId = userId,
            LoungeShowId = showId,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo)
        });
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<Rec>> RecommendationsAsync(int userId, string city, int limit)
    {
        var res = await _factory.CreateAuthenticatedClient(userId, "Audience")
            .GetAsync($"/api/v1/recommendations?city={city}&limit={limit}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
    }

    [Fact]
    public async Task TheDominantGenreWinsOverAOneOffOne()
    {
        // Sáu buổi thể loại 1 và đúng một buổi thể loại 2 trong lịch sử. Với phép hợp thuần tuý thì
        // hai thể loại vào hồ sơ ngang nhau: buổi ứng viên thể loại 2 chấm điểm y hệt buổi thể loại
        // 1, và vì nó diễn sớm hơn nên thắng ở mốc phá hoà và lên đứng đầu.
        var (loungeId, city) = await VenueAsync();
        var userId = await AccountWithNoDeclaredTasteAsync();

        for (var i = 0; i < 6; i++)
        {
            var s = await ShowAsync(loungeId, $"Lich su gu chinh {i}", SeedHelper.GenreId1, 30 + i);
            await SaveToWishlistAsync(userId, s, minutesAgo: 100 + i);
        }

        var oneOff = await ShowAsync(loungeId, "Lich su mua ho ban", SeedHelper.GenreId2, 40);
        await SaveToWishlistAsync(userId, oneOff, minutesAgo: 90);

        // Hai ứng viên chưa từng thấy. Buổi lệch gu diễn SỚM HƠN, nên nếu hai bên hoà điểm thì nó
        // thắng — đó chính là tình huống cần dựng, không dựng được thì bài test không kiểm gì.
        var offTaste = await ShowAsync(loungeId, "Ung vien lech gu", SeedHelper.GenreId2, 10);
        var onTaste = await ShowAsync(loungeId, "Ung vien hop gu", SeedHelper.GenreId1, 20);

        var ids = (await RecommendationsAsync(userId, city, limit: 2)).Select(r => r.Id).ToList();

        ids.IndexOf(onTaste).Should().BeLessThan(ids.IndexOf(offTaste),
            "thể loại người dùng quan tâm sáu lần phải thắng thể loại họ chạm đúng một lần");
    }

    [Fact]
    public async Task ATasteThatIsGenuinelyBroadIsKeptWhole()
    {
        // Nửa còn lại, và là nửa dễ làm hỏng: người thật sự thích hai thể loại ngang nhau thì không
        // được cắt mất một. Quy tắc là "ít nhất một nửa so với thẻ mạnh nhất", nên hai thẻ ngang
        // nhau đều ở lại — cắt theo kiểu chỉ giữ thẻ mạnh nhất sẽ làm hỏng đúng trường hợp này.
        var (loungeId, city) = await VenueAsync();
        var userId = await AccountWithNoDeclaredTasteAsync();

        for (var i = 0; i < 3; i++)
        {
            var a = await ShowAsync(loungeId, $"Lich su A {i}", SeedHelper.GenreId1, 30 + i);
            await SaveToWishlistAsync(userId, a, minutesAgo: 100 + i);
            var b = await ShowAsync(loungeId, $"Lich su B {i}", SeedHelper.GenreId2, 50 + i);
            await SaveToWishlistAsync(userId, b, minutesAgo: 110 + i);
        }

        var candidateA = await ShowAsync(loungeId, "Ung vien A", SeedHelper.GenreId1, 12);
        var candidateB = await ShowAsync(loungeId, "Ung vien B", SeedHelper.GenreId2, 14);

        var ids = (await RecommendationsAsync(userId, city, limit: 6)).Select(r => r.Id).ToList();

        ids.Should().Contain(candidateA);
        ids.Should().Contain(candidateB);
        ids.IndexOf(candidateA).Should().BeLessThan(3,
            "cả hai thể loại đều là gu thật, cả hai đều phải lên đầu");
        ids.IndexOf(candidateB).Should().BeLessThan(3);
    }

    [Fact]
    public async Task ASingleShowInHistoryStillCounts()
    {
        // Trường hợp biên nguy hiểm nhất của mọi quy tắc theo tỉ lệ: người mới chỉ có đúng một buổi
        // trong lịch sử. Thẻ mạnh nhất xuất hiện 1 lần, ngưỡng là 0.5 lần, nên nó ở lại — nếu quy
        // tắc làm rỗng hồ sơ ở đây thì người mới mất sạch cá nhân hoá, đúng nhóm MLACP-321 sinh ra
        // để phục vụ.
        var (loungeId, city) = await VenueAsync();
        var userId = await AccountWithNoDeclaredTasteAsync();

        var only = await ShowAsync(loungeId, "Buoi duy nhat trong lich su", SeedHelper.GenreId1, 30);
        await SaveToWishlistAsync(userId, only, minutesAgo: 60);

        var matching = await ShowAsync(loungeId, "Ung vien hop gu", SeedHelper.GenreId1, 25);
        var unrelated = await ShowAsync(loungeId, "Ung vien lech gu", SeedHelper.GenreId2, 5);

        var ids = (await RecommendationsAsync(userId, city, limit: 2)).Select(r => r.Id).ToList();

        ids.IndexOf(matching).Should().BeLessThan(ids.IndexOf(unrelated),
            "một buổi trong lịch sử vẫn là một căn cứ, không được coi như không biết gì");
    }
}
