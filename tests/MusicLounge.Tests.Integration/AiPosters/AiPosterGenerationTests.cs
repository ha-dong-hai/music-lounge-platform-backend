using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// W02 — AI poster generation gated by SubscriptionPackage.HasAiPoster/MaxAiPostersPerMonth.
/// No Gemini:ApiKey is configured in appsettings.Testing.json, so every generation attempt here
/// exercises the "vendor unavailable" path — exactly what proves the fail-safe design actually
/// holds: a failed attempt must never consume the Owner's monthly quota.
/// POST /api/v1/lounge-shows/{id}/ai-poster | GET .../ai-poster/history
/// </summary>
[Collection("Integration")]
public sealed class AiPosterGenerationTests
{
    private readonly ApiFactory _factory;

    public AiPosterGenerationTests(ApiFactory factory) => _factory = factory;

    private async Task<int> CreateShowAsync(int ownerId = SeedHelper.OwnerId)
    {
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner", SeedHelper.LoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"AiPosterTestShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = "Offline",
            ScheduledStart = SeedHelper.NextShowStart(),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    [Fact]
    public async Task GeneratePoster_AsNonOwnerOfVenue_Returns403()
    {
        var showId = await CreateShowAsync();
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await otherOwnerClient.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GeneratePoster_VendorUnavailable_LogsFailedAttempt_DoesNotConsumeQuota_Returns503()
    {
        var showId = await CreateShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempt = await db.Set<AiPosterGeneration>().FirstOrDefaultAsync(a => a.ShowId == showId);
        attempt.Should().NotBeNull();
        attempt!.Status.Should().Be(AiPosterGenerationStatus.Failed);
        attempt.ImageUrl.Should().BeNull();

        // Quota only counts Succeeded attempts — a Failed one must not touch LoungeShow.PosterUrl either.
        var show = await db.LoungeShows.FirstAsync(s => s.Id == showId);
        show.PosterUrl.Should().BeNull();
        show.PosterByAi.Should().BeFalse();
    }

    [Fact]
    public async Task GetPosterHistory_AfterFailedAttempt_ShowsFailedEntry()
    {
        var showId = await CreateShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        var res = await client.GetAsync($"/api/v1/lounge-shows/{showId}/ai-poster/history");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Failed\"");
    }

    [Fact]
    public async Task GeneratePoster_NhaCungCapHong_KhongTruVaoGioiHanMoiBuoiDien()
    {
        // MLACP-419: truoc day moi lan thu deu bi tinh, nen tren moi truong chua cau hinh nha cung cap (dung nhu Azure
        // hom 16/09) chu phong tra bam 5 lan la KHOA VINH VIEN tinh nang cho buoi dien do — ma chua nhan duoc tam poster
        // nao, trong khi day la tinh nang ho da tra tien trong goi subscription.
        var showId = await CreateShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        for (var i = 0; i < 6; i++)
        {
            var thu = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });
            thu.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
                "nha cung cap hong thi tra loi 'dich vu khong san sang', khong phai 'het luot'");
        }
    }

    [Fact]
    public async Task GeneratePoster_DaTaoDuSoPosterChoBuoiDien_BaoHetLuot()
    {
        var showId = await CreateShowAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // 5 poster ĐÃ TẠO ĐƯỢC (mặc định ai_poster_max_attempts_per_show = 5).
            for (var i = 0; i < 5; i++)
                db.Add(new AiPosterGeneration
                {
                    ShowId = showId,
                    OwnerId = SeedHelper.OwnerId,
                    Status = AiPosterGenerationStatus.Succeeded,
                    Prompt = "poster",
                    ImageUrl = $"/uploads/poster-{i}.png",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
                });
            await db.SaveChangesAsync();
        }
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("giới hạn");
    }

    private sealed record IdResponse(bool Success, int Data);
}
