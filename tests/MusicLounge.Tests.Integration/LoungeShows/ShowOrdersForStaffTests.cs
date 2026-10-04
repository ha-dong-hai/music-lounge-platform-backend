using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-592: nhân viên của đúng phòng trà xem được danh sách khách để đón khách, nhưng KHÔNG thấy email người mua.
/// Chủ phòng trà vẫn thấy đầy đủ; nhân viên phòng trà khác vẫn bị chặn.
/// </summary>
[Collection("Integration")]
public sealed class ShowOrdersForStaffTests
{
    private readonly ApiFactory _factory;

    public ShowOrdersForStaffTests(ApiFactory factory) => _factory = factory;

    private async Task<(Guid ShowId, Guid TicketId)> SeedShowWithOneBuyerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"StaffOrdersShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(9),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(9).AddHours(2)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = show.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(ticket);
        await db.SaveChangesAsync();
        return (show.Id, ticket.Id);
    }

    private static async Task<ShowOrder> RowAsync(HttpResponseMessage res, Guid ticketId)
    {
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await res.Content.ReadFromJsonAsync<Envelope<Paged<ShowOrder>>>())!.Data.Items;
        return items.Single(o => o.TicketId == ticketId);
    }

    [Fact]
    public async Task StaffOfThisVenue_SeesBuyerName_ButNotEmail()
    {
        var (showId, ticketId) = await SeedShowWithOneBuyerAsync();
        var staff = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var row = await RowAsync(await staff.GetAsync($"/api/v1/lounge-shows/{showId}/orders?pageSize=100"), ticketId);

        row.BuyerName.Should().NotBeNullOrWhiteSpace("người đón khách ở cửa cần biết tên");
        row.BuyerEmail.Should().BeNull("nhân viên không cần email người mua để đón khách");
        row.TierName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Owner_StillSeesBuyerEmail()
    {
        var (showId, ticketId) = await SeedShowWithOneBuyerAsync();
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var row = await RowAsync(await owner.GetAsync($"/api/v1/lounge-shows/{showId}/orders?pageSize=100"), ticketId);

        row.BuyerEmail.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task StaffOfAnotherVenue_IsForbidden()
    {
        var (showId, _) = await SeedShowWithOneBuyerAsync();
        var otherStaff = _factory.CreateAuthenticatedClient(SeedHelper.OtherVenueStaffId, "Staff", SeedHelper.OtherLoungeId);

        var res = await otherStaff.GetAsync($"/api/v1/lounge-shows/{showId}/orders");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
    private sealed record ShowOrder(Guid TicketId, string? BuyerName, string? BuyerEmail, string TierName);
}
