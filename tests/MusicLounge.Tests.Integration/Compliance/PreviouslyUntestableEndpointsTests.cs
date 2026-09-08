using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-306. Four data paths ordered by a DateTimeOffset column in the database. SQL Server is
/// fine with that, so nothing was broken in production — but SQLite, the provider this suite runs
/// on, refuses it outright. Any test touching those four endpoints threw NotSupportedException,
/// so no test existed for any of them.
///
/// Almost every defect found in this codebase today was found by a test. These were four places no
/// test could look. That is the reason to fix them, and these tests are the fix paying for itself:
/// before MLACP-306 not one of them could even run.
/// </summary>
[Collection("Integration")]
public sealed class PreviouslyUntestableEndpointsTests
{
    private readonly ApiFactory _factory;

    public PreviouslyUntestableEndpointsTests(ApiFactory factory) => _factory = factory;

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

    [Fact]
    public async Task TheVenuesIFollow_CanBeListed()
    {
        var client = Audience();

        var follow = await client.PostAsync($"/api/v1/follows/lounges/{SeedHelper.LoungeId}", null);
        follow.IsSuccessStatusCode.Should().BeTrue();

        var res = await client.GetAsync("/api/v1/follows/lounges?pageSize=50");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<Envelope<Paged<FollowedLounge>>>();
        body!.Data.Items.Should().Contain(l => l.Id == SeedHelper.LoungeId);
    }

    [Fact]
    public async Task MyWishlist_CanBeListed()
    {
        var client = Audience();

        var add = await client.PostAsync($"/api/v1/wishlist/{SeedHelper.OfflineShowId}", null);
        add.IsSuccessStatusCode.Should().BeTrue();

        var res = await client.GetAsync("/api/v1/wishlist?pageSize=50");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<Envelope<Paged<ShowListItem>>>();
        body!.Data.Items.Should().Contain(s => s.Id == SeedHelper.OfflineShowId);
    }

    [Fact]
    public async Task SearchingWithNoSortOrder_Works()
    {
        // The default branch of the search sort — the most ordinary path through the whole system,
        // and the one that could not be exercised at all.
        var res = await _factory.CreateClient().GetAsync("/api/v1/lounge-shows/search?pageSize=20");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<Envelope<Paged<ShowListItem>>>())!.Data
            .Should().NotBeNull();
    }

    [Fact]
    public async Task SearchResults_ComeBackNewestFirst()
    {
        int older, newer;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tag = Guid.NewGuid().ToString("N")[..12];

            LoungeShow Make(string suffix) => new()
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"SearchOrder-{tag}-{suffix}",
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(11),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(11).AddHours(3)
            };

            var a = Make("a");
            db.LoungeShows.Add(a);
            await db.SaveChangesAsync();
            var b = Make("b");
            db.LoungeShows.Add(b);
            await db.SaveChangesAsync();

            older = a.Id;
            newer = b.Id;

            var res0 = await _factory.CreateClient()
                .GetAsync($"/api/v1/lounge-shows/search?q=SearchOrder-{tag}&pageSize=20");
            res0.StatusCode.Should().Be(HttpStatusCode.OK);
            var items = (await res0.Content.ReadFromJsonAsync<Envelope<Paged<ShowListItem>>>())!.Data.Items;

            var ids = items.Select(i => i.Id).Where(i => i == older || i == newer).ToList();
            ids.Should().HaveCount(2);
            ids[0].Should().Be(newer, "default order is newest first, and that must survive the fix");
        }
    }

    [Fact]
    public async Task TheAdminUserList_CanBeListed()
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync("/api/v1/admin/users?pageSize=20");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<Envelope<Paged<AdminUser>>>();
        body!.Data.Items.Should().NotBeEmpty();
        body.Data.TotalCount.Should().BeGreaterThan(0);

        // Không đòi một tài khoản cụ thể phải nằm ở trang đầu: các test khác trong suite cũng tạo
        // người dùng, và thứ tự là mới nhất trước. Điều cần chốt là trang trả về đúng thứ tự đó.
        body.Data.Items.Select(u => u.Id).Should().BeInDescendingOrder();
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
    private sealed record FollowedLounge(int Id, string Name);
    private sealed record ShowListItem(int Id, string Name);
    private sealed record AdminUser(int Id, string Email);
}
