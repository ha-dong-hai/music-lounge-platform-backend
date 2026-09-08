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
/// MLACP-300. LoungeShow carries two image columns. SetShowPoster and the AI poster generator both
/// write PosterUrl; every DTO read CoverImageUrl, which nothing writes at all. An owner uploaded a
/// poster and no screen anywhere showed it — every show, in every list, returned a null image.
///
/// The tests go through the real endpoints rather than the mapper, because the defect was never in
/// the mapping logic itself: each DTO was individually correct about a column that was always empty.
/// Only an end-to-end path can tell the difference.
/// </summary>
[Collection("Integration")]
public sealed class ShowImageIsActuallyShownTests
{
    private readonly ApiFactory _factory;

    public ShowImageIsActuallyShownTests(ApiFactory factory) => _factory = factory;

    private const string Poster = "/uploads/poster-abc123.png";

    private async Task<int> SeedShowWithPosterAsync(string? posterUrl = Poster, string? cover = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"PosterShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(9),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(9).AddHours(3),
            PosterUrl = posterUrl,
            CoverImageUrl = cover
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    [Fact]
    public async Task TheDetailPage_ShowsThePosterTheOwnerUploaded()
    {
        var showId = await SeedShowWithPosterAsync();

        var res = await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{showId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<Envelope<ShowDetail>>();
        body!.Data.CoverImageUrl.Should().Be(Poster,
            "the owner uploaded a poster — a show page with no image is the visible symptom of this bug");
    }

    [Fact]
    public async Task TheVenueListing_ShowsIt_Too()
    {
        var showId = await SeedShowWithPosterAsync();

        var res = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/by-lounge/{SeedHelper.LoungeId}?pageSize=100");

        var items = (await res.Content.ReadFromJsonAsync<Envelope<Paged<ShowListItem>>>())!.Data.Items;
        items.Single(s => s.Id == showId).CoverImageUrl.Should().Be(Poster);
    }

    [Fact]
    public async Task SearchSuggestions_ShowItToo()
    {
        var showId = await SeedShowWithPosterAsync();
        string name;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            name = (await db.LoungeShows.SingleAsync(s => s.Id == showId)).Name;
        }

        var res = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/suggestions?q={Uri.EscapeDataString(name[..20])}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = (await res.Content.ReadFromJsonAsync<Envelope<List<Suggestion>>>())!.Data;
        items.Should().NotBeEmpty();
        items.Single(s => s.Id == showId).CoverImageUrl.Should().Be(Poster);
    }

    [Fact]
    public async Task AnExplicitCoverImage_StillWins_IfAnythingEverSetsOne()
    {
        // Preference order rather than a straight swap: nothing writes CoverImageUrl today, but if
        // a real cover-image feature is wired later it must not be shadowed by the poster.
        const string cover = "/uploads/cover-xyz789.png";
        var showId = await SeedShowWithPosterAsync(posterUrl: Poster, cover: cover);

        var body = await (await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{showId}"))
            .Content.ReadFromJsonAsync<Envelope<ShowDetail>>();

        body!.Data.CoverImageUrl.Should().Be(cover);
    }

    [Fact]
    public async Task AShowWithNoImageAtAll_StillReturnsNull_NotAnEmptyString()
    {
        var showId = await SeedShowWithPosterAsync(posterUrl: null, cover: null);

        var body = await (await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{showId}"))
            .Content.ReadFromJsonAsync<Envelope<ShowDetail>>();

        body!.Data.CoverImageUrl.Should().BeNull(
            "clients branch on null to decide whether to render a placeholder");
    }

    [Fact]
    public async Task TheModerationQueue_ShowsTheImageAdminsAreSupposedToBeJudging()
    {
        // The site that worried me most: an Admin reviewing a show for approval could not see its
        // image, which is a large part of what there is to review.
        var showId = await SeedShowWithPosterAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = await db.LoungeShows.SingleAsync(s => s.Id == showId);
            show.Status = LoungeShowStatus.Pending;
            db.Add(new EventModeration
            {
                TargetType = ModerationTargetType.Show,
                TargetId = showId,
                SlaDeadline = DateTimeOffset.UtcNow.AddHours(24)
            });
            await db.SaveChangesAsync();
        }

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync("/api/v1/admin/shows/pending?pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(Poster,
            "an Admin approving a show without seeing its image is reviewing half the submission");
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
    private sealed record ShowDetail(int Id, string Name, string? CoverImageUrl);
    private sealed record ShowListItem(int Id, string Name, string? CoverImageUrl);
    private sealed record Suggestion(int Id, string Name, string? CoverImageUrl);
}
