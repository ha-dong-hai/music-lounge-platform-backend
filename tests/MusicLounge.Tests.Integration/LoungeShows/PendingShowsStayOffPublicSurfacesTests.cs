using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// Buổi diễn đang chờ Admin duyệt (Pending) chưa phải nội dung công khai. Tìm kiếm (MLACP-58) và trang
/// chi tiết đã chặn nó từ trước, nhưng bốn đường khác chỉ loại bản nháp nên vẫn để lọt: danh sách theo
/// phòng trà, danh sách theo nghệ sĩ, sơ đồ chỗ ngồi (kèm giá vé), và thêm vào yêu thích. Phát hiện
/// 30/09/2026 khi đo thật: <c>GET /lounge-shows/by-lounge/1</c> gọi không đăng nhập trả 3 buổi Pending.
///
/// Bốn ca dưới đây đều ĐỎ trên mã trước khi sửa (đã chạy với phần src được cất đi) — xem ghi chú commit.
/// Phạm vi KHÔNG kiểm: các đường đã đúng từ trước (duyệt, tìm kiếm, gợi ý, tương tự, đang được quan tâm).
/// </summary>
[Collection("Integration")]
public sealed class PendingShowsStayOffPublicSurfacesTests
{
    private readonly ApiFactory _factory;

    public PendingShowsStayOffPublicSurfacesTests(ApiFactory factory) => _factory = factory;

    private async Task<Guid> SeedShowAsync(LoungeShowStatus status, bool withPerformer = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"ModerationGate-{status}-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = status,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(9),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(9).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        if (withPerformer)
        {
            db.Add(new Performance { LoungeShowId = show.Id, PerformerId = SeedHelper.PerformerId, OrderIndex = 0 });
            await db.SaveChangesAsync();
        }
        return show.Id;
    }

    [Fact]
    public async Task VenuePage_ListsPublishedAndPastShows_ButNotOnesAwaitingReview()
    {
        var pending = await SeedShowAsync(LoungeShowStatus.Pending);
        var draft = await SeedShowAsync(LoungeShowStatus.Draft);
        var published = await SeedShowAsync(LoungeShowStatus.Published);
        var ended = await SeedShowAsync(LoungeShowStatus.Ended);

        var res = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/by-lounge/{SeedHelper.LoungeId}?pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await res.Content.ReadFromJsonAsync<Envelope<Paged<ShowListItem>>>())!.Data.Items
            .Select(s => s.Id).ToList();

        ids.Should().NotBeEmpty("chặn quét trúng số không: danh sách rỗng thì mọi NotContain đều xanh");
        ids.Should().Contain(published);
        ids.Should().Contain(ended, "buổi đã diễn là lịch sử công khai của phòng trà, cổng duyệt không ẩn nó");
        ids.Should().NotContain(draft);
        ids.Should().NotContain(pending, "buổi chưa được Admin duyệt thì sàn chưa đứng tên giới thiệu");
    }

    [Fact]
    public async Task PerformerPage_DoesNotListShowsAwaitingReview()
    {
        var pending = await SeedShowAsync(LoungeShowStatus.Pending, withPerformer: true);
        var published = await SeedShowAsync(LoungeShowStatus.Published, withPerformer: true);

        var res = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/by-performer/{SeedHelper.PerformerId}?pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await res.Content.ReadFromJsonAsync<Envelope<PerformerDetail>>())!.Data.Shows.Items
            .Select(s => s.Id).ToList();

        ids.Should().Contain(published);
        ids.Should().NotContain(pending);
    }

    [Fact]
    public async Task SeatingMapOfAShowAwaitingReview_Is404ForOutsiders_ButReadableByItsVenue()
    {
        var pending = await SeedShowAsync(LoungeShowStatus.Pending);

        var anonymous = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/{pending}/seating-map");
        anonymous.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "sơ đồ chỗ ngồi mang cả giá vé; trang chi tiết của cùng buổi này đã trả 404");

        var audience = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .GetAsync($"/api/v1/lounge-shows/{pending}/seating-map");
        audience.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var owner = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner")
            .GetAsync($"/api/v1/lounge-shows/{pending}/seating-map");
        owner.StatusCode.Should().Be(HttpStatusCode.OK, "chính phòng trà vẫn phải xem được buổi mình đã nộp");
    }

    [Fact]
    public async Task AShowAwaitingReview_CannotBeWishlisted()
    {
        var pending = await SeedShowAsync(LoungeShowStatus.Pending);
        var published = await SeedShowAsync(LoungeShowStatus.Published);
        var audience = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        (await audience.PostAsync($"/api/v1/wishlist/{pending}", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound,
                "lưu được thì danh sách yêu thích thành đường đọc tên, ảnh, giờ diễn của buổi chưa duyệt");

        // Ca đối chứng: cùng lời gọi với buổi đã công bố phải thành công, nếu không ca trên xanh vì lý do khác.
        (await audience.PostAsync($"/api/v1/wishlist/{published}", null))
            .IsSuccessStatusCode.Should().BeTrue();
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
    private sealed record ShowListItem(Guid Id, string Name);
    private sealed record PerformerDetail(Guid Id, string Name, Paged<ShowListItem> Shows);
}
