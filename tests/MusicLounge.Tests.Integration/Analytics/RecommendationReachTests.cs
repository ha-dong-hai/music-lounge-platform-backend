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
/// MLACP-326. Buổi diễn nào ĐƯỢC PHÉP có mặt trong danh sách gợi ý.
///
/// <b>Phòng trà bị đình chỉ vẫn được đem đi mời khách.</b> Không truy vấn buổi diễn nào trong
/// repository lọc theo trạng thái phòng trà. Cổng duyệt BR-01 (MLACP-307) chỉ chặn lúc ĐĂNG, không
/// thu hồi những gì đã đăng — nên khi Admin đình chỉ một phòng trà, hệ thống vẫn chủ động gợi ý
/// buổi diễn của họ và mời người dùng mua vé vào đúng nơi mình vừa đình chỉ. Đình chỉ mà vẫn quảng
/// bá thì việc đình chỉ chẳng có nghĩa gì.
///
/// Chiều ngược lại cũng phải giữ: <b>cảnh cáo không phải đình chỉ</b>. Phòng trà bị nhắc nhở vẫn
/// đang hoạt động và vẫn bán vé; cắt họ khỏi màn hình khám phá là tự ý nâng một lời cảnh cáo thành
/// một hình phạt kinh tế.
///
/// <b>Về bộ lọc thành phố.</b> Ban đầu tôi nghi nó loại oan buổi diễn trực tuyến của phòng trà tỉnh
/// khác. Đã tra chuẩn schema.org và cách các nền tảng thật làm, kết luận ngược lại: buổi trực tuyến
/// KHÔNG có thành phố (địa điểm của nó là <c>VirtualLocation</c>), nên lọc theo thành phố mà không
/// trả về nó là đúng. Chi tiết ở chú thích <c>ShowDiscoverability.ReachableFrom</c>. Hai bài kiểm
/// tra dưới đây chốt giữ đúng hành vi đó.
/// </summary>
[Collection("Integration")]
public sealed class RecommendationReachTests
{
    private readonly ApiFactory _factory;

    public RecommendationReachTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(int Id, string Name);

    private async Task<int> VenueAsync(string city, LoungeStatus status = LoungeStatus.Approved)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = SeedHelper.OwnerId,
            Name = $"RVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = status,
            Address = new VenueAddress { Street = "1 Tam Voi", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return lounge.Id;
    }

    private async Task<int> ShowAsync(
        int loungeId, string name, LoungeShowFormat format = LoungeShowFormat.Offline,
        double daysFromNow = 20)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(daysFromNow);
        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = name,
            Description = "Integration test show",
            Format = format,
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

    private async Task SetVenueStatusAsync(int loungeId, LoungeStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = await db.Lounges.FindAsync(loungeId);
        lounge!.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<int> ListenerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"reach-{Guid.NewGuid():N}@test.com",
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

    private async Task<IReadOnlyList<Rec>> RecommendationsAsync(int userId, string? city)
    {
        var query = city is null ? "limit=50" : $"city={city}&limit=50";
        var res = await _factory.CreateAuthenticatedClient(userId, "Audience")
            .GetAsync($"/api/v1/recommendations?{query}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
    }

    // ---------- ① trạng thái phòng trà ----------

    [Fact]
    public async Task AShowFromASuspendedVenueIsNotRecommended()
    {
        // Admin đình chỉ một phòng trà, nhưng buổi diễn đã đăng thì vẫn nằm nguyên trong kho ứng
        // viên. Hệ thống tiếp tục chủ động mời người dùng mua vé vào đúng nơi mình vừa đình chỉ.
        var city = $"RCity-{Guid.NewGuid():N}"[..20];
        var venue = await VenueAsync(city);
        var show = await ShowAsync(venue, "Phong tra bi dinh chi");
        var ok = await ShowAsync(await VenueAsync(city), "Phong tra binh thuong");

        var userId = await ListenerAsync();
        (await RecommendationsAsync(userId, city)).Select(r => r.Id)
            .Should().Contain(show, "tiền đề: trước khi đình chỉ thì nó vẫn được gợi ý bình thường");

        await SetVenueStatusAsync(venue, LoungeStatus.Suspended);

        var ids = (await RecommendationsAsync(userId, city)).Select(r => r.Id).ToList();
        ids.Should().NotContain(show, "đình chỉ mà vẫn quảng bá thì việc đình chỉ chẳng có nghĩa gì");
        ids.Should().Contain(ok, "phòng trà bình thường không bị ảnh hưởng");
    }

    [Fact]
    public async Task AShowFromALockedOrRejectedVenueIsNotRecommendedEither()
    {
        var city = $"RCity-{Guid.NewGuid():N}"[..20];
        var locked = await VenueAsync(city);
        var rejected = await VenueAsync(city);
        var lockedShow = await ShowAsync(locked, "Phong tra bi khoa");
        var rejectedShow = await ShowAsync(rejected, "Phong tra bi tu choi");

        await SetVenueStatusAsync(locked, LoungeStatus.Locked);
        await SetVenueStatusAsync(rejected, LoungeStatus.Rejected);

        var ids = (await RecommendationsAsync(await ListenerAsync(), city)).Select(r => r.Id).ToList();
        ids.Should().NotContain(lockedShow);
        ids.Should().NotContain(rejectedShow);
    }

    [Fact]
    public async Task AWarnedVenueIsStillRecommended()
    {
        // Nửa còn lại, và là nửa dễ làm quá tay: cảnh cáo KHÔNG phải đình chỉ. Phòng trà bị nhắc
        // nhở vẫn đang hoạt động bình thường và vẫn bán vé — cắt họ khỏi màn hình khám phá là tự ý
        // nâng một lời cảnh cáo thành một hình phạt kinh tế.
        var city = $"RCity-{Guid.NewGuid():N}"[..20];
        var venue = await VenueAsync(city);
        var show = await ShowAsync(venue, "Phong tra bi canh cao");

        await SetVenueStatusAsync(venue, LoungeStatus.Warned);

        (await RecommendationsAsync(await ListenerAsync(), city)).Select(r => r.Id)
            .Should().Contain(show, "cảnh cáo không phải đình chỉ");
    }

    // ---------- ② bộ lọc thành phố: giữ đúng nghĩa "tới được" ----------

    [Fact]
    public async Task AnOfflineShowInAnotherCityIsStillExcluded()
    {
        // Chốt giữ chiều ngược lại: nới cho buổi trực tuyến không được làm hỏng chính bộ lọc. Buổi
        // diễn tại chỗ ở tỉnh khác thì người dùng không tới được, và đó là lý do bộ lọc tồn tại.
        var myCity = $"RCity-{Guid.NewGuid():N}"[..20];
        var farCity = $"RCity-{Guid.NewGuid():N}"[..20];

        await ShowAsync(await VenueAsync(myCity), "Tai cho o day");
        var offlineFar = await ShowAsync(await VenueAsync(farCity), "Tai cho o tinh khac");

        (await RecommendationsAsync(await ListenerAsync(), myCity)).Select(r => r.Id)
            .Should().NotContain(offlineFar, "buổi diễn tại chỗ ở xa thì người dùng không tới được");
    }

    [Fact]
    public async Task WithoutACityFilterEverythingIsStillOffered()
    {
        // Không lọc thành phố thì không có gì bị loại vì lý do địa lý.
        var farCity = $"RCity-{Guid.NewGuid():N}"[..20];
        var offlineFar = await ShowAsync(await VenueAsync(farCity), "Tai cho o tinh khac");

        (await RecommendationsAsync(await ListenerAsync(), city: null)).Select(r => r.Id)
            .Should().Contain(offlineFar);
    }
}
