using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-294. Four endpoints that did not exist, over machinery that already did:
/// GetByLoungeAsync, GetByPerformerAsync and GetDistinctCitiesAsync were implemented in the
/// repository and called from nowhere, and FilterOptionsDto and ShowOrderDto were shaped and never
/// built. Written, wired to the database, and unreachable.
///
/// The one that carries risk is the orders list: it holds buyers' names and email addresses, so
/// "Owner" as a role is not the check that matters — being the owner of THAT venue is.
/// </summary>
[Collection("Integration")]
public sealed class MissingListEndpointsTests
{
    private readonly ApiFactory _factory;

    public MissingListEndpointsTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedPublishedShowAsync(int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = $"ListEndpointShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(8),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(8).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    // ---------- danh sách theo venue ----------

    [Fact]
    public async Task ShowsOfAVenue_AreListedPublicly_AndOnlyThatVenuesShows()
    {
        var mine = await SeedPublishedShowAsync(SeedHelper.LoungeId);
        var elsewhere = await SeedPublishedShowAsync(SeedHelper.OtherLoungeId);

        var res = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/by-lounge/{SeedHelper.LoungeId}?pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK, "khán giả xem trang venue trước khi mua vé");

        var items = (await res.Content.ReadFromJsonAsync<Envelope<Paged<ShowListItem>>>())!.Data.Items;
        items.Should().Contain(s => s.Id == mine);
        items.Should().NotContain(s => s.Id == elsewhere,
            "a venue page listing another venue's shows would be worse than having no page");
    }

    [Fact]
    public async Task AnOversizedPageRequest_IsClamped_NotHonoured()
    {
        // Endpoint công khai: pageSize=100000 là một truy vấn tốn kém mà bất kỳ ai cũng gửi được.
        var res = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/by-lounge/{SeedHelper.LoungeId}?pageSize=100000");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<Envelope<Paged<ShowListItem>>>())!.Data.PageSize
            .Should().BeLessThanOrEqualTo(100);
    }

    // ---------- trang nghệ sĩ ----------

    [Fact]
    public async Task PerformerPage_ReturnsThePerformerAndTheirShows()
    {
        var res = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/by-performer/{SeedHelper.PerformerId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<Envelope<PerformerDetail>>();
        body!.Data.Id.Should().Be(SeedHelper.PerformerId);
        body.Data.Name.Should().NotBeNullOrWhiteSpace();
        body.Data.Shows.Should().NotBeNull();
    }

    [Fact]
    public async Task PerformerPage_ForSomeoneWhoDoesNotExist_Is404_NotAnEmptyPage()
    {
        var res = await _factory.CreateClient().GetAsync("/api/v1/lounge-shows/by-performer/999999");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "an empty page would read as \"this artist has no shows\" rather than \"no such artist\"");
    }

    // ---------- bộ lọc ----------

    [Fact]
    public async Task FilterOptions_ComeBackInOneCall_AndListOnlyCitiesThatActuallyHaveShows()
    {
        var res = await _factory.CreateClient().GetAsync("/api/v1/lounge-shows/filter-options");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var options = (await res.Content.ReadFromJsonAsync<Envelope<FilterOptions>>())!.Data;

        options.Genres.Should().NotBeEmpty();
        options.Moods.Should().NotBeEmpty();
        options.Atmospheres.Should().NotBeEmpty();
        options.Cities.Should().OnlyHaveUniqueItems()
            .And.NotContain(c => string.IsNullOrWhiteSpace(c),
                "a blank entry in a filter dropdown is a row of empty space the user can select");
    }

    // ---------- đơn hàng của một buổi diễn ----------

    [Fact]
    public async Task Orders_AreVisibleToTheVenueOwner_AndToNobodyElse()
    {
        var showId = await SeedPublishedShowAsync(SeedHelper.LoungeId);

        var owner = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner")
            .GetAsync($"/api/v1/lounge-shows/{showId}/orders");
        owner.StatusCode.Should().Be(HttpStatusCode.OK);

        var admin = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync($"/api/v1/lounge-shows/{showId}/orders");
        admin.StatusCode.Should().Be(HttpStatusCode.OK);

        // The check that matters: this list carries buyers' names and email addresses, so holding
        // the Owner role is not enough — it has to be this venue's owner.
        var otherOwner = await _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner")
            .GetAsync($"/api/v1/lounge-shows/{showId}/orders");
        otherOwner.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var audience = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .GetAsync($"/api/v1/lounge-shows/{showId}/orders");
        audience.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var anonymous = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/{showId}/orders");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Orders_ShowARealPurchaseWithTheBuyerOnIt()
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var buyer = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var hold = await (await buyer.PostAsJsonAsync("/api/v1/tickets/holds",
                new { PriceId = SeedHelper.TicketPriceId, Quantity = 1 }))
            .Content.ReadFromJsonAsync<Envelope<HoldData>>();
        var purchase = await (await buyer.PostAsJsonAsync("/api/v1/tickets/purchase",
                new { HoldId = hold!.Data.HoldId }))
            .Content.ReadFromJsonAsync<Envelope<PurchaseData>>();

        await buyer.GetAsync(
            $"/api/v1/payments/vnpay/callback?vnp_TxnRef={purchase!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(purchase.Data.Amount * 100)}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var showId = (await db.Set<Ticket>().SingleAsync(t => t.Id == purchase.Data.TicketIds[0])).ShowId;

        var res = await owner.GetAsync($"/api/v1/lounge-shows/{showId}/orders?pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = (await res.Content.ReadFromJsonAsync<Envelope<Paged<ShowOrder>>>())!.Data.Items;
        var row = orders.Single(o => o.TicketId == purchase.Data.TicketIds[0]);

        row.BuyerName.Should().NotBeNullOrWhiteSpace("đón khách thì phải biết tên người mua");
        row.TierName.Should().NotBeNullOrWhiteSpace();
        row.PricePaid.Should().BeGreaterThan(0);
        row.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task MyTickets_Works_AndWasNotCoveredByAnyTestUntilNow()
    {
        // Not strictly part of MLACP-294, but it shares the defect this task exposed: GetByBuyerAsync
        // ordered by a DateTimeOffset in the database, which SQLite refuses. The endpoint has existed
        // and been reachable the whole time; nothing exercised it, so the failure stayed invisible
        // and would only ever have appeared as a 500 in an environment running SQLite.
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .GetAsync("/api/v1/tickets/my?pageSize=20");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<Envelope<Paged<object>>>())!.Data.Should().NotBeNull();
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
    private sealed record ShowListItem(int Id, string Name);
    private sealed record PerformerDetail(int Id, string Name, Paged<ShowListItem> Shows);
    private sealed record NamedOption(int Id, string Name);
    private sealed record FilterOptions(
        IReadOnlyList<NamedOption> Genres, IReadOnlyList<NamedOption> Moods,
        IReadOnlyList<NamedOption> Atmospheres, IReadOnlyList<string> Cities);
    private sealed record ShowOrder(
        Guid TicketId, string? BuyerName, string? BuyerEmail, string TierName, string PriceName,
        decimal PricePaid, string Status, string PurchaseChannel,
        DateTimeOffset CreatedAt, DateTimeOffset? CheckedInAt);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
    private sealed record PurchaseData(
        int PaymentId, string OrderId, decimal Amount, string PaymentUrl, Guid[] TicketIds);
}
