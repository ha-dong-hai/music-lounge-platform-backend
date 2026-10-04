using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-580. Khối "Chương trình in riêng cho bạn" ở trang chủ chỉ có chữ khi buổi diễn chưa có poster — chủ dự án
/// 03/10/2026: "không có hình ảnh thì thật thiếu sự trực quan". Gợi ý phải mang theo ảnh phòng trà để giao diện có
/// hình thay thế, và ảnh đó phải nằm ở trường RIÊNG (loungeImageUrl) chứ không lẫn vào coverImageUrl — giao diện cần
/// biết đó là ảnh phòng trà để ghi chú thích cho đúng.
/// </summary>
[Collection("Integration")]
public sealed class RecommendationCarriesVenueImageTests
{
    private readonly ApiFactory _factory;

    public RecommendationCarriesVenueImageTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(Guid Id, string? CoverImageUrl, string? LoungeImageUrl);

    private async Task<(Guid ShowId, string City)> ShowAtVenueAsync(string? venueImage, string? poster)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var city = $"ICity-{Guid.NewGuid():N}"[..20];
        var owner = new User { Email = $"v580-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id,
            Name = $"IVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            PrimaryImageUrl = venueImage,
            Address = new VenueAddress { Street = "1 Con Ve", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddDays(10);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = "Dem co anh", Description = "Integration test show",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3), PosterUrl = poster
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = SeedHelper.GenreId1 });
        await db.SaveChangesAsync();
        return (show.Id, city);
    }

    private async Task<Rec> RecommendationAsync(Guid showId, string city)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .GetAsync($"/api/v1/recommendations?city={city}&limit=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
        data.Should().NotBeEmpty("không có gợi ý nào thì phép kiểm không kiểm được gì");
        return data.Single(r => r.Id == showId);
    }

    [Fact]
    public async Task AShowWithoutPosterStillCarriesTheVenueImage()
    {
        var (showId, city) = await ShowAtVenueAsync("https://img.test/phong-tra.jpg", poster: null);

        var rec = await RecommendationAsync(showId, city);

        rec.CoverImageUrl.Should().BeNull("buổi chưa có poster — không được lấy ảnh phòng trà giả làm poster");
        rec.LoungeImageUrl.Should().Be("https://img.test/phong-tra.jpg");
    }

    [Fact]
    public async Task PosterAndVenueImageStaySeparate()
    {
        var (showId, city) = await ShowAtVenueAsync("https://img.test/phong-tra.jpg", "https://img.test/poster.jpg");

        var rec = await RecommendationAsync(showId, city);

        rec.CoverImageUrl.Should().Be("https://img.test/poster.jpg");
        rec.LoungeImageUrl.Should().Be("https://img.test/phong-tra.jpg");
    }
}
