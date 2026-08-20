using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Recommendations;

/// <summary>
/// RecomputeUserEventScoresJob populates user_event_scores — the collaborative-filtering training
/// matrix MLNetRecommendationService reads from — which nothing wrote to before this fix, silently
/// disabling 30% of the documented hybrid recommendation formula. Also covers the
/// UpdateAiPreferencesCommandHandler remove+add-in-one-transaction race fixed alongside it.
/// PUT /api/v1/me/preferences
/// </summary>
[Collection("Integration")]
public sealed class RecommendationDataPipelineTests
{
    private readonly ApiFactory _factory;

    public RecommendationDataPipelineTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RecomputeUserEventScoresJob_AggregatesRealSignalsIntoUserEventScore()
    {
        const int userId = SeedHelper.AudienceId;
        int showId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId, Name = $"EventScoreTestShow-{Guid.NewGuid():N}",
                Description = "test", Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(10)
            };
            db.Add(show);
            await db.SaveChangesAsync();
            showId = show.Id;

            // Attended (via a Confirmed ticket) — strongest signal.
            db.Add(new Ticket
            {
                Id = Guid.NewGuid(), BuyerId = userId, PriceId = SeedHelper.TicketPriceId,
                TierId = SeedHelper.TicketTierId, ShowId = showId, Status = TicketStatus.Confirmed,
                PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow
            });

            // Wishlisted.
            db.Add(new ShowWishlist { UserId = userId, LoungeShowId = showId, CreatedAt = DateTimeOffset.UtcNow });

            // Explicit rating.
            db.Add(new LoungeShowRating
            {
                UserId = userId, LoungeShowId = showId, Score = 4, IsRemoved = false,
                CreatedAt = DateTimeOffset.UtcNow
            });

            // Donation — via a fresh Performance row linked to this show.
            var performance = new Performance
            {
                LoungeShowId = showId, PerformerId = SeedHelper.PerformerId,
                Role = PerformerRole.Main, OrderIndex = 1, AcceptsDonation = true
            };
            db.Add(performance);
            await db.SaveChangesAsync();

            db.Add(new Donation
            {
                DonorUserId = userId, PerformanceId = performance.Id,
                Gross = 50_000m, Net = 45_000m, Status = DonationStatus.PerformerPaid,
                PaymentConfirmedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
            });

            // View + click-intent behaviour logs.
            db.Add(new UserBehaviourLog
            {
                UserId = userId, LoungeShowId = showId, Action = BehaviourAction.ViewEvent,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.Add(new UserBehaviourLog
            {
                UserId = userId, LoungeShowId = showId, Action = BehaviourAction.ClickTicket,
                CreatedAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<RecomputeUserEventScoresJob>();
            var act = () => job.ExecuteAsync(new JobCancellationToken(false));
            await act.Should().NotThrowAsync();
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await verifyDb.Set<UserEventScore>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ShowId == showId);

        row.Should().NotBeNull("attended + wishlisted + rated + donated + viewed should all count as real signal");
        row!.Score.Should().BeGreaterThan(0m);
        row.Breakdown.Should().NotBeNullOrEmpty();
        row.Breakdown.Should().Contain("\"attended\":true");
        row.Breakdown.Should().Contain("\"wishlist\":true");
        row.Breakdown.Should().Contain("\"donated\":true");

        // Running twice must update the existing composite-key row, not throw a duplicate-key error.
        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<RecomputeUserEventScoresJob>();
            var act = () => job.ExecuteAsync(new JobCancellationToken(false));
            await act.Should().NotThrowAsync();
        }
    }

    [Fact]
    public async Task UpdatePreferences_CalledTwiceWithOverlappingIds_Returns204BothTimes()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var body = new
        {
            GenreIds = new[] { SeedHelper.GenreId1, SeedHelper.GenreId2 },
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            EnableAiConsent = true
        };

        var first = await client.PutAsJsonAsync("/api/v1/me/preferences", body);
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Same exact ids again — maximum overlap for the remove-then-add race: every row being
        // removed has an id that's about to be re-inserted.
        var second = await client.PutAsJsonAsync("/api/v1/me/preferences", body);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var genreCount = await db.Set<UserFavouriteGenre>()
            .CountAsync(g => g.UserId == SeedHelper.AudienceId);
        genreCount.Should().Be(2);
    }
}
