using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-591: chủ và nhân viên phòng trà không chấm được buổi diễn của chính phòng trà mình, kể cả khi có vé đã soát
/// (vì người soát vé chính là họ). Chủ vẫn chấm được buổi của phòng trà khác như mọi khán giả.
/// </summary>
[Collection("Integration")]
public sealed class RateShowVenueInsiderTests
{
    private readonly ApiFactory _factory;

    public RateShowVenueInsiderTests(ApiFactory factory) => _factory = factory;

    // Buổi diễn đã kết thúc (còn trong cửa sổ đánh giá) ở phòng trà mẫu + một vé ĐÃ SOÁT của người chấm.
    private async Task<Guid> SeedEndedShowWithUsedTicketAsync(Guid buyerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"InsiderRatingShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Ended,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-2),
            ActualEnd = DateTimeOffset.UtcNow.AddDays(-1),
            RatingOpenUntil = DateTimeOffset.UtcNow.AddDays(6)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        db.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = buyerId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = show.Id,
            Status = TicketStatus.Used,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
        });
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<int> RatingCountAsync(Guid showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<LoungeShowRating>().CountAsync(r => r.LoungeShowId == showId);
    }

    [Fact]
    public async Task Owner_RatingOwnVenueShow_Returns403_AndSavesNothing()
    {
        var showId = await SeedEndedShowWithUsedTicketAsync(SeedHelper.OwnerId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/rate", new { Score = 5, Comment = "tuyệt vời" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RatingCountAsync(showId)).Should().Be(0);
    }

    [Fact]
    public async Task Staff_RatingOwnVenueShow_Returns403_AndSavesNothing()
    {
        var showId = await SeedEndedShowWithUsedTicketAsync(SeedHelper.StaffId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/rate", new { Score = 5, Comment = "tuyệt vời" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RatingCountAsync(showId)).Should().Be(0);
    }

    [Fact]
    public async Task OwnerOfAnotherVenue_RatingThisShow_IsAllowed()
    {
        var showId = await SeedEndedShowWithUsedTicketAsync(SeedHelper.OtherOwnerId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/rate", new { Score = 4, Comment = "hay" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RatingCountAsync(showId)).Should().Be(1);
    }

    [Fact]
    public async Task Audience_RatingWithUsedTicket_StillAllowed()
    {
        var showId = await SeedEndedShowWithUsedTicketAsync(SeedHelper.AudienceId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/rate", new { Score = 5, Comment = "hay" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
