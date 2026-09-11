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

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-329. Đình chỉ một phòng trà thì buổi diễn của họ phải biến khỏi MỌI đường khám phá công
/// khai, không chỉ khỏi gợi ý.
///
/// MLACP-326 mới vá đúng đường gợi ý. Rà hết repository thì còn sáu truy vấn công khai nữa không lọc
/// trạng thái phòng trà — toàn bộ đều <c>AllowAnonymous</c>. Nên tình trạng thật là: phòng trà bị
/// đình chỉ biến mất khỏi danh bạ (MLACP-307 đã chặn qua <c>VenueLifecycle.IsPubliclyVisible</c>),
/// nhưng buổi diễn của họ vẫn nằm nguyên trong duyệt, tìm kiếm, gợi ý liên quan, gợi ý tự động
/// điền, danh sách theo nghệ sĩ và cả danh sách thành phố dùng để lọc.
///
/// Nói cách khác: nền tảng giấu cái biển hiệu đi nhưng vẫn tiếp tục bán vé vào cửa.
///
/// <b>Chiều ngược lại cũng phải giữ.</b> Cảnh cáo không phải đình chỉ, và Owner vẫn phải thấy buổi
/// diễn của chính mình để còn xử lý — nếu đình chỉ mà họ cũng không mở được danh sách của mình thì
/// không có đường nào khắc phục.
/// </summary>
[Collection("Integration")]
public sealed class SuspendedVenueDisappearsFromDiscoveryTests
{
    private readonly ApiFactory _factory;

    public SuspendedVenueDisappearsFromDiscoveryTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Page<T>(IReadOnlyList<T> Items, int TotalCount);
    private sealed record Item(int Id, string Name);
    private sealed record Suggestion(int Id, string Name);
    private sealed record FilterOptions(IReadOnlyList<string> Cities);
    private sealed record PerformerDetail(int Id, Page<Item> Shows);

    private async Task<int> VenueAsync(string city)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = freshOwner.Id,
            Name = $"SusVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Dinh Chi", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return lounge.Id;
    }

    private async Task<int> ShowAsync(int loungeId, string name, int? performerId = null)
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

        db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = SeedHelper.GenreId1 });
        if (performerId is { } p)
            db.Add(new Performance { LoungeShowId = show.Id, PerformerId = p });
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task SetStatusAsync(int loungeId, LoungeStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = await db.Lounges.FindAsync(loungeId);
        lounge!.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<Item>> PagedAsync(string url)
    {
        var res = await _factory.CreateClient().GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<Page<Item>>>())!.Data.Items;
    }

    // ---------- sáu đường công khai ----------

    [Fact]
    public async Task BrowsingDoesNotListItAnyMore()
    {
        var venue = await VenueAsync($"XCity-{Guid.NewGuid():N}"[..20]);
        var show = await ShowAsync(venue, $"Duyet-{Guid.NewGuid():N}"[..20]);

        (await PagedAsync("/api/v1/lounge-shows?page=1&pageSize=200"))
            .Select(i => i.Id).Should().Contain(show, "tiền đề: trước khi đình chỉ nó vẫn hiện");

        await SetStatusAsync(venue, LoungeStatus.Suspended);

        (await PagedAsync("/api/v1/lounge-shows?page=1&pageSize=200"))
            .Select(i => i.Id).Should().NotContain(show);
    }

    [Fact]
    public async Task SearchDoesNotFindItAnyMore()
    {
        var venue = await VenueAsync($"XCity-{Guid.NewGuid():N}"[..20]);
        var name = $"TimKiem-{Guid.NewGuid():N}"[..20];
        var show = await ShowAsync(venue, name);

        (await PagedAsync($"/api/v1/lounge-shows/search?keyword={name}&pageSize=50"))
            .Select(i => i.Id).Should().Contain(show, "tiền đề");

        await SetStatusAsync(venue, LoungeStatus.Suspended);

        (await PagedAsync($"/api/v1/lounge-shows/search?keyword={name}&pageSize=50"))
            .Select(i => i.Id).Should().NotContain(show);
    }

    [Fact]
    public async Task AutocompleteDoesNotSuggestItAnyMore()
    {
        // Gợi ý tự động điền là đường dễ quên nhất vì nó không giống một "danh sách" — nhưng nó
        // vẫn dẫn thẳng người dùng tới trang bán vé.
        var venue = await VenueAsync($"XCity-{Guid.NewGuid():N}"[..20]);
        var name = $"GoiY-{Guid.NewGuid():N}"[..20];
        var show = await ShowAsync(venue, name);

        async Task<IReadOnlyList<int>> SuggestAsync()
        {
            var res = await _factory.CreateClient()
                .GetAsync($"/api/v1/lounge-shows/suggestions?q={name}");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var data = (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Suggestion>>>())!.Data;
            return data.Select(s => s.Id).ToList();
        }

        (await SuggestAsync()).Should().Contain(show, "tiền đề");

        await SetStatusAsync(venue, LoungeStatus.Suspended);

        (await SuggestAsync()).Should().NotContain(show);
    }

    [Fact]
    public async Task TheVenuesOwnShowListIsEmptyForOutsiders()
    {
        var venue = await VenueAsync($"XCity-{Guid.NewGuid():N}"[..20]);
        var show = await ShowAsync(venue, $"TheoPhongTra-{Guid.NewGuid():N}"[..20]);

        (await PagedAsync($"/api/v1/lounge-shows/by-lounge/{venue}?pageSize=50"))
            .Select(i => i.Id).Should().Contain(show, "tiền đề");

        await SetStatusAsync(venue, LoungeStatus.Suspended);

        (await PagedAsync($"/api/v1/lounge-shows/by-lounge/{venue}?pageSize=50"))
            .Select(i => i.Id).Should().NotContain(show);
    }

    [Fact]
    public async Task ThePerformersShowListDropsItToo()
    {
        var venue = await VenueAsync($"XCity-{Guid.NewGuid():N}"[..20]);
        var show = await ShowAsync(venue, $"TheoNgheSi-{Guid.NewGuid():N}"[..20], SeedHelper.PerformerId);

        // Endpoint nay tra ve ho so nghe si, danh sach buoi dien nam trong truong Shows.
        async Task<IReadOnlyList<int>> ByPerformerAsync()
        {
            var res = await _factory.CreateClient()
                .GetAsync($"/api/v1/lounge-shows/by-performer/{SeedHelper.PerformerId}?pageSize=200");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var data = (await res.Content.ReadFromJsonAsync<Envelope<PerformerDetail>>())!.Data;
            return data.Shows.Items.Select(i => i.Id).ToList();
        }

        (await ByPerformerAsync()).Should().Contain(show, "tiền đề");

        await SetStatusAsync(venue, LoungeStatus.Suspended);

        (await ByPerformerAsync()).Should().NotContain(show);
    }

    [Fact]
    public async Task RelatedShowsDoNotPointAtItAnyMore()
    {
        // "Buổi diễn tương tự" trên trang chi tiết — đường này dẫn người đang xem sang thẳng một
        // buổi khác, nên để lọt phòng trà bị đình chỉ vào đây là mời họ đi đúng chỗ vừa bị cấm.
        var city = $"XCity-{Guid.NewGuid():N}"[..20];
        var good = await VenueAsync(city);
        var bad = await VenueAsync(city);
        var anchor = await ShowAsync(good, $"Goc-{Guid.NewGuid():N}"[..20]);
        var related = await ShowAsync(bad, $"LienQuan-{Guid.NewGuid():N}"[..20]);

        async Task<IReadOnlyList<int>> SimilarAsync()
        {
            var res = await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{anchor}/similar");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var data = (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Item>>>())!.Data;
            return data.Select(i => i.Id).ToList();
        }

        (await SimilarAsync()).Should().Contain(related, "tiền đề");

        await SetStatusAsync(bad, LoungeStatus.Suspended);

        (await SimilarAsync()).Should().NotContain(related);
    }

    [Fact]
    public async Task ACityWithOnlySuspendedVenuesDisappearsFromTheFilter()
    {
        // Danh sách thành phố dùng để dựng bộ lọc. Một thành phố chỉ còn phòng trà bị đình chỉ mà
        // vẫn nằm đó thì người dùng chọn vào sẽ nhận màn hình trống — một lựa chọn dẫn tới hư không.
        var city = $"XCity-{Guid.NewGuid():N}"[..20];
        var venue = await VenueAsync(city);
        await ShowAsync(venue, $"ThanhPho-{Guid.NewGuid():N}"[..20]);

        async Task<IReadOnlyList<string>> CitiesAsync()
        {
            var res = await _factory.CreateClient().GetAsync("/api/v1/lounge-shows/filter-options");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await res.Content.ReadFromJsonAsync<Envelope<FilterOptions>>())!.Data.Cities;
        }

        (await CitiesAsync()).Should().Contain(city, "tiền đề");

        await SetStatusAsync(venue, LoungeStatus.Suspended);

        (await CitiesAsync()).Should().NotContain(city);
    }

    // ---------- chiều ngược lại ----------

    [Fact]
    public async Task AWarnedVenueIsStillFullyVisible()
    {
        // Cảnh cáo là một vết ghi lại, không phải lệnh dừng — đúng như VenueLifecycle đã định
        // nghĩa từ MLACP-307. Cắt họ khỏi mọi đường khám phá là biến một lời nhắc thành án phạt.
        var venue = await VenueAsync($"XCity-{Guid.NewGuid():N}"[..20]);
        var show = await ShowAsync(venue, $"CanhCao-{Guid.NewGuid():N}"[..20]);

        await SetStatusAsync(venue, LoungeStatus.Warned);

        (await PagedAsync("/api/v1/lounge-shows?page=1&pageSize=200"))
            .Select(i => i.Id).Should().Contain(show);
    }

    [Fact]
    public async Task TheOwnerCanStillSeeTheirOwnShowsAfterSuspension()
    {
        // Bắt buộc phải giữ. Đình chỉ mà Owner cũng không mở được danh sách của chính mình thì họ
        // không có đường nào để xử lý và khắc phục.
        var venue = await VenueAsync($"XCity-{Guid.NewGuid():N}"[..20]);
        var show = await ShowAsync(venue, $"CuaToi-{Guid.NewGuid():N}"[..20]);

        await SetStatusAsync(venue, LoungeStatus.Suspended);

        // MLACP-377: VenueAsync() gio tao mot chu MOI cho moi phong tra — tra dung chu tu DB.
        int ownerId;
        using (var scope = _factory.Services.CreateScope())
            ownerId = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Lounges.AsNoTracking().Single(l => l.Id == venue).OwnerId;

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner", venue)
            .GetAsync("/api/v1/lounge-shows/mine?page=1&pageSize=200");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = (await res.Content.ReadFromJsonAsync<Envelope<Page<Item>>>())!.Data.Items;
        items.Select(i => i.Id).Should().Contain(show,
            "Owner phải thấy buổi diễn của mình để còn khắc phục");
    }
}
