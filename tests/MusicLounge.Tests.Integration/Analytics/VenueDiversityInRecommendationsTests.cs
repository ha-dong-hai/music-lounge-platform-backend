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
/// MLACP-320. Một phòng trà không được chiếm trọn danh sách gợi ý.
///
/// Danh sách được xếp thuần theo mức hợp gu, nên một phòng trà đăng mười buổi diễn cùng thể loại sẽ
/// lấp kín toàn bộ danh sách của người thích thể loại đó. Người dùng mở màn hình gợi ý ra và tưởng
/// nền tảng chỉ có mỗi chỗ đó.
///
/// Đây là vấn đề các sàn thương mại điện tử đã gặp và đã có cách xử: đặt trần tỉ lệ theo người bán,
/// để không ai chiếm quá một phần kết quả. Hai bên cùng được lợi — khán giả thấy nhiều lựa chọn
/// hơn, và phòng trà nhỏ không bị đẩy khỏi màn hình khám phá chỉ vì đăng ít buổi diễn hơn.
/// </summary>
[Collection("Integration")]
public sealed class VenueDiversityInRecommendationsTests
{
    private readonly ApiFactory _factory;

    public VenueDiversityInRecommendationsTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(int Id, string Name, string LoungeName);

    private async Task<int> VenueAsync(string city, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = freshOwner.Id,
            Name = name,
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Da Dang", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return lounge.Id;
    }

    /// <param name="startOffsetDays">
    /// Buổi diễn cùng thể loại thì điểm hợp gu bằng nhau, nên thứ tự do mốc phá hoà quyết định —
    /// và mốc đó là "sắp diễn trước". Muốn dựng đúng tình huống một phòng trà chiếm hết danh sách
    /// thì buổi của nó phải diễn sớm hơn hẳn; để cùng dải ngày thì hai bên tự đan xen và bài kiểm
    /// tra không kiểm được gì.
    /// </param>
    private async Task ShowsAsync(int loungeId, int count, string prefix, int startOffsetDays = 15)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        for (var i = 0; i < count; i++)
        {
            var start = DateTimeOffset.UtcNow.AddDays(startOffsetDays + i);
            var show = new LoungeShow
            {
                LoungeId = loungeId,
                Name = $"{prefix}-{i}",
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(3)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();

            // Cùng thể loại với gu người nghe, nên nếu không có trần thì tất cả đều lên đầu.
            db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = SeedHelper.GenreId1 });
            await db.SaveChangesAsync();
        }
    }

    private async Task<int> ListenerWhoLikesGenreOneAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"div-{Guid.NewGuid():N}@test.com",
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

    private async Task<IReadOnlyList<Rec>> RecommendationsAsync(int userId, string city, int limit)
    {
        var res = await _factory.CreateAuthenticatedClient(userId, "Audience")
            .GetAsync($"/api/v1/recommendations?city={city}&limit={limit}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
    }

    [Fact]
    public async Task OneVenueCannotFillTheWholeList()
    {
        // Phòng trà lớn đăng 10 buổi cùng thể loại, phòng trà nhỏ đăng 2. Không có trần thì cả 10
        // suất đều thuộc phòng lớn và phòng nhỏ không bao giờ được nhìn thấy.
        var city = $"DCity-{Guid.NewGuid():N}"[..20];
        var big = await VenueAsync(city, $"PhongTraLon-{Guid.NewGuid():N}");
        var small = await VenueAsync(city, $"PhongTraNho-{Guid.NewGuid():N}");

        // Buổi của phòng lớn diễn sớm hơn hẳn, nên không có trần thì chúng chiếm trọn 9 suất.
        await ShowsAsync(big, 10, "Lon", startOffsetDays: 15);
        await ShowsAsync(small, 2, "Nho", startOffsetDays: 60);

        var userId = await ListenerWhoLikesGenreOneAsync();
        var recs = await RecommendationsAsync(userId, city, limit: 9);

        var fromBig = recs.Count(r => r.Name.StartsWith("Lon"));
        fromBig.Should().BeLessThan(recs.Count,
            "không được để một phòng trà chiếm trọn danh sách");
        recs.Should().Contain(r => r.Name.StartsWith("Nho"),
            "phòng trà nhỏ phải có mặt, nếu không khán giả tưởng nền tảng chỉ có mỗi chỗ kia");
    }

    [Fact]
    public async Task TheCapIsProportionalToHowManySlotsThereAre()
    {
        // Trần là max(2, limit/3): không ai chiếm quá khoảng một phần ba danh sách.
        var city = $"DCity-{Guid.NewGuid():N}"[..20];
        var big = await VenueAsync(city, $"PhongTraLon-{Guid.NewGuid():N}");
        var other1 = await VenueAsync(city, $"PhongTraA-{Guid.NewGuid():N}");
        var other2 = await VenueAsync(city, $"PhongTraB-{Guid.NewGuid():N}");

        await ShowsAsync(big, 10, "Lon", startOffsetDays: 15);
        await ShowsAsync(other1, 5, "A", startOffsetDays: 60);
        await ShowsAsync(other2, 5, "B", startOffsetDays: 80);

        var userId = await ListenerWhoLikesGenreOneAsync();
        var recs = await RecommendationsAsync(userId, city, limit: 9);

        recs.Count(r => r.Name.StartsWith("Lon")).Should().BeLessThanOrEqualTo(3,
            "9 suất thì mỗi phòng trà nhiều nhất 3");
    }

    [Fact]
    public async Task TheCapNeverShrinksTheList()
    {
        // Cùng nguyên tắc với việc loại trừ thứ đã có: trần chỉ để sắp xếp lại, không được làm
        // danh sách ngắn đi. Chỉ có một phòng trà trong thành phố này, nên nếu trần cắt cứng thì
        // danh sách sẽ chỉ còn 2 mục thay vì 6.
        var city = $"DCity-{Guid.NewGuid():N}"[..20];
        var only = await VenueAsync(city, $"PhongTraDuyNhat-{Guid.NewGuid():N}");
        await ShowsAsync(only, 6, "Duy");

        var userId = await ListenerWhoLikesGenreOneAsync();
        var recs = await RecommendationsAsync(userId, city, limit: 6);

        recs.Should().HaveCount(6,
            "chỉ có một phòng trà thì vẫn phải hiện đủ, thà kém đa dạng còn hơn thiếu lựa chọn");
    }

    [Fact]
    public async Task TheBestMatchesStillComeFirstWithinTheCap()
    {
        // Trần không được làm hỏng thứ tự: nó chỉ giới hạn số suất mỗi phòng trà, còn trong phạm vi
        // đó thì buổi hợp gu nhất vẫn lên trước.
        var city = $"DCity-{Guid.NewGuid():N}"[..20];
        var venue = await VenueAsync(city, $"PhongTra-{Guid.NewGuid():N}");
        await ShowsAsync(venue, 3, "Hop");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var start = DateTimeOffset.UtcNow.AddDays(30);
            db.LoungeShows.Add(new LoungeShow
            {
                LoungeId = venue,
                Name = "KhongHop",
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(3)
            });
            await db.SaveChangesAsync();
        }

        var userId = await ListenerWhoLikesGenreOneAsync();
        var recs = await RecommendationsAsync(userId, city, limit: 10);

        var ids = recs.Select(r => r.Name).ToList();
        ids.IndexOf("KhongHop").Should().Be(ids.Count - 1,
            "buổi không khớp thể loại vẫn phải nằm cuối");
    }
}
