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
/// MLACP-330. Người dùng nói "không quan tâm" thì danh sách gợi ý phải ĐỔI THẬT.
///
/// Trước đây hệ thống không có bất kỳ tín hiệu tiêu cực nào: mọi giá trị <c>BehaviourAction</c> đều
/// tích cực hoặc trung tính, <c>UserEventScore</c> chỉ cộng dồn, và cách duy nhất người dùng sửa
/// được gợi ý là vào sửa lại toàn bộ sở thích tự khai. Một suy đoán sai không có đường nào gỡ.
///
/// Và sở thích tiêu cực <b>không suy ra ngầm được</b>: người dùng chỉ bấm vào, chỉ xem, chỉ mua thứ
/// họ thấy thú vị, nên thứ họ không thích không để lại dấu vết nào. Không hỏi thì không bao giờ biết.
///
/// <b>Vì sao bộ test này tập trung vào "đổi thật".</b> Mozilla đo được 62,3% người dùng thấy các nút
/// điều khiển kiểu này chẳng thay đổi được gì. Một nút "không quan tâm" không có tác dụng thật thì
/// tệ hơn là không có nút — nó là lời hứa không có cơ chế, đúng lớp lỗi codebase này đã dính ba lần.
/// Nên mọi bài dưới đây đều đo danh sách TRƯỚC và SAU, chứ không chỉ kiểm rằng dòng dữ liệu được ghi.
/// </summary>
[Collection("Integration")]
public sealed class NotInterestedActuallyChangesTheFeedTests
{
    private readonly ApiFactory _factory;

    public NotInterestedActuallyChangesTheFeedTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(int Id, string Name);
    private sealed record MutedLounge(int Id, string Name);

    private async Task<(int LoungeId, string City)> VenueAsync(string? city = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        city ??= $"NCity-{Guid.NewGuid():N}"[..20];
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = freshOwner.Id,
            Name = $"NVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Khong Quan Tam", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, city);
    }

    private async Task<int> ShowAsync(int loungeId, string name, params int[] genreIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(20);
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

        foreach (var g in genreIds)
            db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = g });
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<int> ListenerAsync(params int[] favouriteGenreIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"noint-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia",
            Role = UserRole.Audience,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            AiConsent = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        foreach (var g in favouriteGenreIds)
            db.Add(new UserFavouriteGenre { UserId = user.Id, GenreId = g });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private HttpClient Client(int userId) => _factory.CreateAuthenticatedClient(userId, "Audience");

    private async Task<IReadOnlyList<int>> RecommendationsAsync(int userId, string city)
    {
        var res = await Client(userId).GetAsync($"/api/v1/recommendations?city={city}&limit=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
        return data.Select(r => r.Id).ToList();
    }

    private async Task MuteAsync(int userId, int loungeId)
    {
        var res = await Client(userId).PostAsync($"/api/v1/mutes/lounges/{loungeId}", null);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task DislikeGenreAsync(int userId, int genreId, params int[] favouriteGenreIds)
    {
        var res = await Client(userId).PutAsJsonAsync("/api/v1/me/preferences", new
        {
            GenreIds = favouriteGenreIds,
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            EnableAiConsent = false,
            DislikedGenreIds = new[] { genreId }
        });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------- tắt tiếng phòng trà ----------

    [Fact]
    public async Task MutingAVenueRemovesItsShowsFromTheFeedImmediately()
    {
        var (muted, city) = await VenueAsync();
        var (other, _) = await VenueAsync(city);
        var hiddenShow = await ShowAsync(muted, "Cua phong tra bi tat tieng", SeedHelper.GenreId1);
        var keptShow = await ShowAsync(other, "Cua phong tra khac", SeedHelper.GenreId1);

        var userId = await ListenerAsync(SeedHelper.GenreId1);

        (await RecommendationsAsync(userId, city))
            .Should().Contain(hiddenShow, "tiền đề: trước khi tắt tiếng nó vẫn được gợi ý");

        await MuteAsync(userId, muted);

        var after = await RecommendationsAsync(userId, city);
        after.Should().NotContain(hiddenShow, "người dùng vừa nói thẳng là không muốn thấy nữa");
        after.Should().Contain(keptShow, "chỉ phòng trà bị tắt tiếng biến mất, không phải cả danh sách");
    }

    [Fact]
    public async Task MutingCutsHardEvenWhenNothingElseIsLeftToShow()
    {
        // Khác hẳn mọi phép đẩy xuống cuối khác. Những phép kia dựa trên SUY ĐOÁN nên "thà hiện
        // thừa còn hơn màn hình trống" là đúng. Ở đây người dùng đã tự tay nói không — hiện lại
        // là phớt lờ đúng điều họ vừa nói, và biến cái nút thành một lời hứa suông.
        var (only, city) = await VenueAsync();
        await ShowAsync(only, "Buoi duy nhat trong thanh pho", SeedHelper.GenreId1);

        var userId = await ListenerAsync(SeedHelper.GenreId1);
        await MuteAsync(userId, only);

        (await RecommendationsAsync(userId, city)).Should().BeEmpty(
            "tôn trọng lựa chọn của người dùng quan trọng hơn việc lấp đầy màn hình");
    }

    [Fact]
    public async Task UnmutingBringsItBack()
    {
        // Một lựa chọn không gỡ được thì không phải quyền kiểm soát, mà là một cái bẫy.
        var (venue, city) = await VenueAsync();
        var show = await ShowAsync(venue, "Se duoc bat lai", SeedHelper.GenreId1);

        var userId = await ListenerAsync(SeedHelper.GenreId1);
        await MuteAsync(userId, venue);
        (await RecommendationsAsync(userId, city)).Should().NotContain(show);

        var res = await Client(userId).DeleteAsync($"/api/v1/mutes/lounges/{venue}");
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RecommendationsAsync(userId, city)).Should().Contain(show);
    }

    [Fact]
    public async Task MutingAVenueAlsoStopsFollowingIt()
    {
        // Vừa theo dõi vừa tắt tiếng là một trạng thái vô nghĩa, và nó gửi hai chỉ thị trái ngược
        // cho cùng một phòng trà.
        var (venue, _) = await VenueAsync();
        var userId = await ListenerAsync();

        var follow = await Client(userId).PostAsync($"/api/v1/follows/lounges/{venue}", null);
        follow.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await MuteAsync(userId, venue);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Follows.AnyAsync(f => f.UserId == userId && f.LoungeId == venue))
            .Should().BeFalse("bấm tắt tiếng là đã nói rõ ý, không cần hỏi lại");
    }

    [Fact]
    public async Task MutingDoesNotHideTheVenueFromSearch()
    {
        // Tắt tiếng là cá nhân hoá, không phải kiểm duyệt. Nó chặn thứ hệ thống CHỦ ĐỘNG đẩy tới;
        // người dùng vẫn phải tự tìm ra được nếu họ chủ động đi tìm.
        var (venue, _) = await VenueAsync();
        var name = $"VanTimDuoc-{Guid.NewGuid():N}"[..24];
        var show = await ShowAsync(venue, name, SeedHelper.GenreId1);

        var userId = await ListenerAsync(SeedHelper.GenreId1);
        await MuteAsync(userId, venue);

        var res = await Client(userId).GetAsync($"/api/v1/lounge-shows/search?keyword={name}&pageSize=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await res.Content.ReadFromJsonAsync<Envelope<PagedItems>>())!.Data;

        page.Items.Select(i => i.Id).Should().Contain(show,
            "tắt tiếng không được biến thành cấm tìm kiếm");
    }

    private sealed record PagedItems(IReadOnlyList<Rec> Items);

    [Fact]
    public async Task TheMutedListIsReadableSoItCanBeUndone()
    {
        var (venue, _) = await VenueAsync();
        var userId = await ListenerAsync();
        await MuteAsync(userId, venue);

        var res = await Client(userId).GetAsync("/api/v1/mutes/lounges");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<MutedLounge>>>())!.Data;

        data.Select(m => m.Id).Should().Contain(venue);
    }

    // ---------- thể loại không thích ----------

    [Fact]
    public async Task DislikingAGenreRemovesShowsThatAreOnlyThatGenre()
    {
        var (venue, city) = await VenueAsync();
        var disliked = await ShowAsync(venue, "Chi mot the loai bi che", SeedHelper.GenreId2);
        var kept = await ShowAsync(venue, "The loai duoc thich", SeedHelper.GenreId1);

        var userId = await ListenerAsync(SeedHelper.GenreId1);

        (await RecommendationsAsync(userId, city)).Should().Contain(disliked, "tiền đề");

        await DislikeGenreAsync(userId, SeedHelper.GenreId2, SeedHelper.GenreId1);

        var after = await RecommendationsAsync(userId, city);
        after.Should().NotContain(disliked);
        after.Should().Contain(kept);
    }

    [Fact]
    public async Task AShowThatMixesADislikedGenreWithALikedOneSurvives()
    {
        // Nửa dễ làm quá tay nhất. Buổi gắn cả Bolero lẫn Jazz, với người không thích Jazz nhưng mê
        // Bolero, vẫn là một buổi đáng giới thiệu. Cắt nó là suy diễn từ một câu nói hẹp thành một
        // lệnh cấm rộng.
        var (venue, city) = await VenueAsync();
        var mixed = await ShowAsync(venue, "Vua co the loai thich vua co the ghet",
            SeedHelper.GenreId1, SeedHelper.GenreId2);

        var userId = await ListenerAsync(SeedHelper.GenreId1);
        await DislikeGenreAsync(userId, SeedHelper.GenreId2, SeedHelper.GenreId1);

        (await RecommendationsAsync(userId, city)).Should().Contain(mixed,
            "chỉ cắt khi TOÀN BỘ thể loại của buổi diễn đều bị chê");
    }

    [Fact]
    public async Task AGenreCannotBeLikedAndDislikedAtTheSameTime()
    {
        // Trạng thái mâu thuẫn phải bị chặn ở đường ghi, không để nó tồn tại rồi đẩy phần xử lý
        // xuống cho mọi nơi đọc.
        var userId = await ListenerAsync();

        var res = await Client(userId).PutAsJsonAsync("/api/v1/me/preferences", new
        {
            GenreIds = new[] { SeedHelper.GenreId1 },
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            EnableAiConsent = false,
            DislikedGenreIds = new[] { SeedHelper.GenreId1, SeedHelper.GenreId2 }
        });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await db.Set<UserDislikedGenre>()
            .AnyAsync(d => d.UserId == userId && d.GenreId == SeedHelper.GenreId1))
            .Should().BeFalse("sở thích tự khai là bản thắng, phần mâu thuẫn bị bỏ đi");
        (await db.Set<UserDislikedGenre>()
            .AnyAsync(d => d.UserId == userId && d.GenreId == SeedHelper.GenreId2))
            .Should().BeTrue("phần không mâu thuẫn vẫn được ghi bình thường");
    }

    // ---------- ranh giới ----------

    [Fact]
    public async Task ErasingTheAccountRemovesTheseChoicesToo()
    {
        // Đây cũng là lựa chọn người dùng tự khai, nên nó đi cùng nhóm với sở thích yêu thích: xoá
        // tài khoản thì xoá hết.
        var (venue, _) = await VenueAsync();
        var userId = await ListenerAsync();
        await MuteAsync(userId, venue);
        await DislikeGenreAsync(userId, SeedHelper.GenreId2);

        var res = await Client(userId)
            .PostAsJsonAsync("/api/v1/me/data-erasure", new { CurrentPassword = (string?)null });
        res.IsSuccessStatusCode.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<LoungeMute>().CountAsync(m => m.UserId == userId)).Should().Be(0);
        (await db.Set<UserDislikedGenre>().CountAsync(d => d.UserId == userId)).Should().Be(0);
    }

    [Fact]
    public async Task AGuestIsUnaffectedBecauseTheyHaveNoSuchChoices()
    {
        // Khách vãng lai không có hồ sơ để đọc, và không được tạo một cái sau lưng họ.
        var (venue, city) = await VenueAsync();
        var show = await ShowAsync(venue, "Khach van thay", SeedHelper.GenreId1);

        var res = await _factory.CreateClient()
            .GetAsync($"/api/v1/recommendations?city={city}&limit=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
        data.Select(r => r.Id).Should().Contain(show);
    }
}
