using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Livestreams.Jobs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// Đợt-2 audit: LoungeShowStatus is written from 13 sites across 7 feature folders. The five inside
/// the LoungeShows folder guard their own source status; the four livestream-driven ones guarded
/// only <c>livestream.Status</c> and wrote <c>show.Status</c> unconditionally.
///
/// CancelLoungeShow deliberately permits cancelling a show whose livestream is merely Scheduled or
/// Reconnecting (only Live blocks), which made this reachable:
///
///   encoder disconnects → livestream Reconnecting, timeout job scheduled for +5 min
///   → Owner cancels the show → tickets cancelled, 100% refunds requested
///   → timeout job fires → show flipped to Ended, ActualEnd and RatingOpenUntil set
///
/// so a cancelled, fully-refunded show read as completed: counted in owner analytics, open for
/// audience ratings, and feeding the completed-show count that picks a venue's settlement tier.
/// </summary>
[Collection("Integration")]
public sealed class ShowLifecycleGuardTests
{
    private readonly ApiFactory _factory;

    public ShowLifecycleGuardTests(ApiFactory factory) => _factory = factory;

    private async Task<(int LivestreamId, DateTimeOffset DisconnectedAt)> SeedReconnectingLivestreamAsync(
        int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var disconnectedAt = DateTimeOffset.UtcNow.AddMinutes(-6);
        var livestream = new Livestream
        {
            LoungeShowId = showId,
            Provider = "mux",
            ProviderRef = $"test-{Guid.NewGuid():N}",
            Status = LivestreamStatus.Reconnecting,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            DisconnectedAt = disconnectedAt
        };
        db.Livestreams.Add(livestream);
        await db.SaveChangesAsync();

        return (livestream.Id, disconnectedAt);
    }

    [Fact]
    public async Task ReconnectTimeout_OnCancelledShow_LeavesShowCancelled()
    {
        var (livestreamId, disconnectedAt) = await SeedReconnectingLivestreamAsync(SeedHelper.CancelledShowId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seededShow = await db.LoungeShows.SingleAsync(s => s.Id == SeedHelper.CancelledShowId);
            seededShow.Status.Should().Be(LoungeShowStatus.Cancelled, "test premise");
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<LivestreamReconnectTimeoutJob>();
            await job.ExecuteAsync(livestreamId, disconnectedAt);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var show = await db.LoungeShows.SingleAsync(s => s.Id == SeedHelper.CancelledShowId);
            show.Status.Should().Be(LoungeShowStatus.Cancelled,
                "a cancelled, refunded show must never be resurrected as a completed one");
            show.ActualEnd.Should().BeNull();
            show.RatingOpenUntil.Should().BeNull(
                "ratings must not open on a show that never happened");

            // The livestream side still has to complete — the guard is on the show transition only.
            var livestream = await db.Livestreams.SingleAsync(l => l.Id == livestreamId);
            livestream.Status.Should().Be(LivestreamStatus.Failed);
        }
    }

    [Fact]
    public async Task ReconnectTimeout_OnLiveShow_StillEndsTheShow()
    {
        int showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"ReconnectGuard-{Guid.NewGuid():N}",
                Description = "Integration test show",
                Status = LoungeShowStatus.Ongoing,
                ScheduledStart = DateTimeOffset.UtcNow.AddHours(-2),
                ScheduledEnd = DateTimeOffset.UtcNow.AddHours(1),
                ActualStart = DateTimeOffset.UtcNow.AddHours(-2)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            showId = show.Id;
        }

        var (livestreamId, disconnectedAt) = await SeedReconnectingLivestreamAsync(showId);

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<LivestreamReconnectTimeoutJob>();
            await job.ExecuteAsync(livestreamId, disconnectedAt);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = await db.LoungeShows.SingleAsync(s => s.Id == showId);
            show.Status.Should().Be(LoungeShowStatus.Ended,
                "the guard must only block terminal shows — a genuinely ongoing one still ends here");
            show.ActualEnd.Should().NotBeNull();
            show.RatingOpenUntil.Should().NotBeNull();
        }
    }
}
