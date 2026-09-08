using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// Đợt-4 audit, two layers over the same defect: an offline show whose Owner never pressed "End"
/// stayed Published forever, leaving tickets refundable long after both settlement tranches had
/// paid the venue. AutoEndStaleShowsJob closes those shows; CancelTicket's own time check is the
/// second layer, deliberately independent of that job running — this codebase has had five jobs
/// silently dead from a missing DI registration.
/// </summary>
[Collection("Integration")]
public sealed class AutoEndStaleShowsTests
{
    private readonly ApiFactory _factory;

    public AutoEndStaleShowsTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedShowAsync(
        LoungeShowStatus status, DateTimeOffset scheduledStart, DateTimeOffset? scheduledEnd)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"StaleShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = status,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<AutoEndStaleShowsJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    [Fact]
    public async Task Job_PublishedShowLongPastScheduledEnd_IsEndedAndBackdated()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-10);
        var end = start.AddHours(3);
        var showId = await SeedShowAsync(LoungeShowStatus.Published, start, end);

        await RunJobAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = await db.LoungeShows.SingleAsync(s => s.Id == showId);

        show.Status.Should().Be(LoungeShowStatus.Ended);
        show.ActualEnd.Should().BeCloseTo(end, TimeSpan.FromSeconds(1),
            "ActualEnd is backdated to the scheduled end, not to whenever the job noticed — a late " +
            "run must not hand the venue a longer apparent show than it actually ran");
        show.RatingOpenUntil.Should().NotBeNull();
        show.ActualStart.Should().BeNull(
            "nobody confirmed the show started, and inventing a start time would fabricate a " +
            "completion ratio out of nothing");
    }

    [Fact]
    public async Task Job_ShowStillWithinGracePeriod_IsLeftAlone()
    {
        // Ended 1 hour ago, default grace is 6 hours — a show running late must not be cut off.
        var start = DateTimeOffset.UtcNow.AddHours(-4);
        var end = DateTimeOffset.UtcNow.AddHours(-1);
        var showId = await SeedShowAsync(LoungeShowStatus.Ongoing, start, end);

        await RunJobAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = await db.LoungeShows.SingleAsync(s => s.Id == showId);

        show.Status.Should().Be(LoungeShowStatus.Ongoing);
        show.ActualEnd.Should().BeNull();
    }

    [Fact]
    public async Task Job_CancelledShow_IsNeverTouched()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-10);
        var showId = await SeedShowAsync(LoungeShowStatus.Cancelled, start, start.AddHours(3));

        await RunJobAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = await db.LoungeShows.SingleAsync(s => s.Id == showId);

        show.Status.Should().Be(LoungeShowStatus.Cancelled,
            "Cancelled is terminal — the same invariant LoungeShowLifecycle enforces everywhere else");
        show.ActualEnd.Should().BeNull();
    }

    [Fact]
    public async Task CancelTicket_OnOverduePublishedShow_IsRefusedEvenIfTheJobNeverRan()
    {
        // Second layer: this must hold without AutoEndStaleShowsJob having run at all, so the
        // ticket is still attached to a show sitting at Published well past its scheduled end.
        var start = DateTimeOffset.UtcNow.AddDays(-10);
        var showId = await SeedShowAsync(LoungeShowStatus.Published, start, start.AddHours(3));

        Guid ticketId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = new Payment
            {
                OrderId = $"AES-{Guid.NewGuid():N}"[..30],
                GrossAmount = 100_000m,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "TicketHold", ReferenceId = "0",
                PaidAt = start, CreatedAt = start
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                BuyerId = SeedHelper.AudienceId,
                PriceId = SeedHelper.TicketPriceId,
                TierId = SeedHelper.TicketTierId,
                ShowId = showId,
                PaymentId = payment.Id,
                Status = TicketStatus.Confirmed,
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = start
            };
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await client.PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.UnprocessableEntity,
            "the show factually finished 10 days ago — refunding now would claw money back from a " +
            "venue that has already been settled");
    }
}
