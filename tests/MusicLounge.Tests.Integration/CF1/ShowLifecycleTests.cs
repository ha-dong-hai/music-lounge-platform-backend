using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// D13 (Reschedule/ChangeFormat), W15/W21/W24 offline Start/End, W26 CancelLoungeShow-với-refund
/// POST /api/v1/lounge-shows/{id}/reschedule | PUT .../format | POST .../start | .../end | .../cancel
/// </summary>
[Collection("Integration")]
public sealed class ShowLifecycleTests
{
    private readonly ApiFactory _factory;

    public ShowLifecycleTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedPublishedShowAsync(
        LoungeShowFormat format = LoungeShowFormat.Offline,
        DateTimeOffset? scheduledStart = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new MusicLounge.Domain.Entities.LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"LifecycleTestShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = format,
            Status = LoungeShowStatus.Published,
            ScheduledStart = scheduledStart ?? DateTimeOffset.UtcNow.AddDays(20),
            ScheduledEnd = (scheduledStart ?? DateTimeOffset.UtcNow.AddDays(20)).AddHours(2),
            VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<Guid> SeedConfirmedPhysicalTicketAsync(int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tier = new TicketTier
        {
            LoungeShowId = showId, Name = "Physical", AccessType = AccessType.Physical,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Standard", Price = 100_000m,
            PurchaseChannel = PurchaseChannel.Both,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(19)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"LC-{Guid.NewGuid():N}"[..30],
            GrossAmount = 100_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = showId,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        return ticket.Id;
    }

    // ─── D13 Reschedule ───────────────────────────────────────────────────────

    [Fact]
    public async Task Reschedule_ToFarEnoughDate_Returns204AndNotifiesTicketHolders()
    {
        var showId = await SeedPublishedShowAsync();
        await SeedConfirmedPhysicalTicketAsync(showId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync(
            $"/api/v1/lounge-shows/{showId}/reschedule",
            new { NewScheduledStart = DateTimeOffset.UtcNow.AddDays(25) });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notif = db.Notifications.FirstOrDefault(
            n => n.UserId == SeedHelper.AudienceId && n.Type == NotificationType.EventReminder
                && n.ReferenceId == showId.ToString());
        notif.Should().NotBeNull();
    }

    [Fact]
    public async Task Reschedule_ToTooSoonDate_Returns422()
    {
        var showId = await SeedPublishedShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync(
            $"/api/v1/lounge-shows/{showId}/reschedule",
            new { NewScheduledStart = DateTimeOffset.UtcNow.AddDays(1) });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── D13 ChangeFormat ─────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeFormat_OfflineToOnline_RefundsPhysicalTicketsAt100Percent()
    {
        var showId = await SeedPublishedShowAsync();
        var ticketId = await SeedConfirmedPhysicalTicketAsync(showId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounge-shows/{showId}/format", new { NewFormat = "Online" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await db.Tickets.FindAsync(ticketId);
        ticket!.Status.Should().Be(TicketStatus.Cancelled);

        var refund = db.RefundRequests.FirstOrDefault(r => r.PaymentId == ticket.PaymentId);
        refund.Should().NotBeNull();
        refund!.RefundPercentage.Should().Be(100m);
        refund.AmountRequested.Should().Be(100_000m);
    }

    // ─── W26 CancelLoungeShow (huỷ cả show) ──────────────────────────────────

    [Fact]
    public async Task CancelLoungeShow_WithConfirmedTickets_RefundsAllAndNotifies()
    {
        var showId = await SeedPublishedShowAsync();
        var ticketId = await SeedConfirmedPhysicalTicketAsync(showId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsync($"/api/v1/lounge-shows/{showId}/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = await db.LoungeShows.FindAsync(showId);
        show!.Status.Should().Be(LoungeShowStatus.Cancelled);

        var ticket = await db.Tickets.FindAsync(ticketId);
        ticket!.Status.Should().Be(TicketStatus.Cancelled);

        var refund = db.RefundRequests.FirstOrDefault(r => r.PaymentId == ticket.PaymentId);
        refund.Should().NotBeNull();
        refund!.RefundPercentage.Should().Be(100m);

        var notif = db.Notifications.FirstOrDefault(
            n => n.UserId == SeedHelper.AudienceId && n.ReferenceId == showId.ToString());
        notif.Should().NotBeNull();
    }

    // ─── W15/W24 Start/End offline show ──────────────────────────────────────

    [Fact]
    public async Task StartThenEnd_OfflineShow_SetsActualTimesAndRatingWindow()
    {
        var showId = await SeedPublishedShowAsync();
        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var startRes = await staffClient.PostAsync($"/api/v1/lounge-shows/{showId}/start", null);
        startRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = await db.LoungeShows.FindAsync(showId);
            show!.Status.Should().Be(LoungeShowStatus.Ongoing);
            show.ActualStart.Should().NotBeNull();
        }

        var endRes = await staffClient.PostAsync($"/api/v1/lounge-shows/{showId}/end", null);
        endRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var endedShow = await verifyDb.LoungeShows.FindAsync(showId);
        endedShow!.Status.Should().Be(LoungeShowStatus.Ended);
        endedShow.ActualEnd.Should().NotBeNull();
        endedShow.RatingOpenUntil.Should().Be(endedShow.ActualEnd!.Value.AddDays(7));
    }

    [Fact]
    public async Task StartOfflineShow_WrongVenueStaff_Returns403()
    {
        var showId = await SeedPublishedShowAsync();
        var wrongStaffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", 999);

        var res = await wrongStaffClient.PostAsync($"/api/v1/lounge-shows/{showId}/start", null);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── §6.13 Rating window ──────────────────────────────────────────────────

    [Fact]
    public async Task RateShow_AfterRatingWindowExpired_Returns422()
    {
        int showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = new MusicLounge.Domain.Entities.LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"ExpiredRatingShow-{Guid.NewGuid():N}",
                Description = "test",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Ended,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(-10),
                ActualEnd = DateTimeOffset.UtcNow.AddDays(-8),
                RatingOpenUntil = DateTimeOffset.UtcNow.AddDays(-1) // expired yesterday
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            showId = show.Id;

            db.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                BuyerId = SeedHelper.AudienceId,
                PriceId = SeedHelper.TicketPriceId,
                TierId = SeedHelper.TicketTierId,
                ShowId = showId,
                Status = TicketStatus.Used,
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/rate", new { Score = 5, Comment = "late" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
