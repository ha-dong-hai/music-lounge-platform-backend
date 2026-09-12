using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// MLACP-388. Tạo hạng vé chỉ chạy khi buổi diễn còn Draft, và đó là đường duy nhất tạo hạng vé. Buổi diễn đã đăng mà
/// phải chuyển sang online (MLACP-383 hoàn 100% vé vào cửa) thì không thể có hạng vé livestream — không bán được vé xem
/// online. Nay: được thêm hạng vé livestream sau khi đăng nếu hình thức là Online/Hybrid và đã có livestream, nhưng giá
/// chỉ mở bán sau khi Admin duyệt. Mỗi bài một phòng trà riêng.
/// </summary>
[Collection("Integration")]
public sealed class LivestreamTierAfterPublishTests
{
    private const string AwaitingReview = "chờ duyệt";

    private readonly ApiFactory _factory;

    public LivestreamTierAfterPublishTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int ShowId);

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private HttpClient Owner(Venue v) => _factory.CreateAuthenticatedClient(v.OwnerId, "Owner", v.LoungeId);
    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

    /// <param name="goOnline">Chuyển Offline → Online qua API thật (MLACP-383), như chủ phòng trà làm.</param>
    private async Task<Venue> PublishedShowAsync(bool goOnline, bool withLivestream)
    {
        Venue venue;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var owner = new User { Email = $"t388-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
            db.Users.Add(owner);
            await db.SaveChangesAsync();
            var lounge = new MusicLoungeEntity
            {
                OwnerId = owner.Id, Name = $"Venue388-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
                Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
            };
            db.Add(lounge);
            await db.SaveChangesAsync();
            var show = new LoungeShow
            {
                LoungeId = lounge.Id, Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18], Description = "MLACP-388",
                Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(3)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            venue = new Venue(owner.Id, lounge.Id, show.Id);
        }

        if (goOnline)
            (await Owner(venue).PutAsJsonAsync($"/api/v1/lounge-shows/{venue.ShowId}/format", new { NewFormat = "Online" }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

        if (withLivestream)
        {
            using var scope = _factory.Services.CreateScope();
            var db = Db(scope);
            db.Add(new Livestream { LoungeShowId = venue.ShowId, Status = LivestreamStatus.Scheduled });
            await db.SaveChangesAsync();
        }
        return venue;
    }

    private Task<HttpResponseMessage> AddTierAsync(Venue venue, string accessType, string name)
        => Owner(venue).PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = venue.ShowId, Name = name, Description = (string?)null, AccessType = accessType,
            ZoneId = (int?)null, TotalCapacity = (int?)null,
            Prices = new[]
            {
                new
                {
                    Name = "Vé xem online", Price = 80_000m, Quota = (int?)null, PurchaseChannel = "Online",
                    SaleStart = DateTimeOffset.UtcNow.AddHours(-1), SaleEnd = (DateTimeOffset?)null
                }
            }
        });

    private static async Task<int> DataIdAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetInt32();
    }

    private async Task<(int TierId, int PriceId)> AddLivestreamTierAsync(Venue venue, string name)
    {
        var res = await AddTierAsync(venue, "Livestream", name);
        res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());
        var tierId = await DataIdAsync(res);
        using var scope = _factory.Services.CreateScope();
        var priceId = (await Db(scope).Set<TicketPrice>().AsNoTracking().SingleAsync(p => p.TierId == tierId)).Id;
        return (tierId, priceId);
    }

    private Task<HttpResponseMessage> HoldAsync(int priceId)
        => Audience().PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = priceId, Quantity = 1 });

    private Task<HttpResponseMessage> ReviewAsync(int tierId, string decision, string? note)
        => Admin().PostAsJsonAsync($"/api/v1/moderations/ticket-tiers/{tierId}/review", new { Decision = decision, ReviewNote = note });

    [Fact]
    public async Task AShowThatWentOnline_CanAddALivestreamTier_ButItWaitsForReviewBeforeSelling()
    {
        var venue = await PublishedShowAsync(goOnline: true, withLivestream: true);
        var tierName = $"Online {Guid.NewGuid():N}"[..16];

        var (tierId, priceId) = await AddLivestreamTierAsync(venue, tierName);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            (await db.Set<TicketPrice>().AsNoTracking().SingleAsync(p => p.Id == priceId)).IsActive
                .Should().BeFalse("a price nobody has reviewed must not be on sale");
            (await db.Set<EventModeration>().AsNoTracking().AnyAsync(m =>
                    m.TargetType == ModerationTargetType.TicketTier && m.TargetId == tierId && m.AdminDecision == null))
                .Should().BeTrue("the new tier enters the Admin review queue");
        }

        var hold = await HoldAsync(priceId);
        hold.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await hold.Content.ReadAsStringAsync()).Should().Contain(AwaitingReview);

        (await (await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{venue.ShowId}")).Content.ReadAsStringAsync())
            .Should().NotContain(tierName, "buyers must not see a tier they cannot buy");
    }

    [Fact]
    public async Task OnceApproved_TheTierSells_AndTheOwnerIsTold()
    {
        var venue = await PublishedShowAsync(goOnline: true, withLivestream: true);
        var (tierId, priceId) = await AddLivestreamTierAsync(venue, "Xem online");

        (await ReviewAsync(tierId, "Approved", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await HoldAsync(priceId)).StatusCode.Should().Be(HttpStatusCode.Created, "an approved tier is on sale");
        using var scope = _factory.Services.CreateScope();
        (await Db(scope).Notifications.AsNoTracking().AnyAsync(n =>
                n.UserId == venue.OwnerId && n.Type == NotificationType.ModerationResult
                && n.ReferenceId == venue.ShowId.ToString() && n.Title == "Hạng vé livestream đã được duyệt"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ARejectedTier_StaysOffSale_AndTheOwnerIsToldWhy()
    {
        var venue = await PublishedShowAsync(goOnline: true, withLivestream: true);
        var (tierId, priceId) = await AddLivestreamTierAsync(venue, "Xem online");

        (await ReviewAsync(tierId, "Rejected", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a rejection must say what to fix");
        (await ReviewAsync(tierId, "Rejected", "Giá không khớp mô tả")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await HoldAsync(priceId)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var scope = _factory.Services.CreateScope();
        (await Db(scope).Notifications.AsNoTracking().AnyAsync(n =>
                n.UserId == venue.OwnerId && n.Title == "Hạng vé livestream bị từ chối" && n.Body.Contains("Giá không khớp mô tả")))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ATierOfAShowThatWasCancelled_CannotBeApproved()
    {
        var venue = await PublishedShowAsync(goOnline: true, withLivestream: true);
        var (tierId, _) = await AddLivestreamTierAsync(venue, "Xem online");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            (await db.LoungeShows.SingleAsync(s => s.Id == venue.ShowId)).Status = LoungeShowStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        (await ReviewAsync(tierId, "Approved", null)).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "opening sales for a show that will not happen sells nothing");
    }

    [Fact]
    public async Task ATierCanOnlyBeReviewedOnce()
    {
        var venue = await PublishedShowAsync(goOnline: true, withLivestream: true);
        var (tierId, _) = await AddLivestreamTierAsync(venue, "Xem online");

        (await ReviewAsync(tierId, "Approved", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReviewAsync(tierId, "Rejected", "đổi ý")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AfterPublishing_AnEntryTierIsStillLocked()
    {
        var venue = await PublishedShowAsync(goOnline: true, withLivestream: true);

        var res = await AddTierAsync(venue, "Physical", "Vào cửa thêm");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "entry tiers and published prices stay as buyers were promised");
    }

    [Fact]
    public async Task AnOfflineShow_CannotAddALivestreamTierAfterPublishing()
    {
        // Co san mot livestream de chi con chot hinh thuc chan duoc — neu khong, chot "phai co livestream" se chan thay va
        // bai nay xanh ca khi chot hinh thuc bi tat.
        var venue = await PublishedShowAsync(goOnline: false, withLivestream: true);

        var res = await AddTierAsync(venue, "Livestream", "Xem online");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("online hoặc hybrid");
    }

    [Fact]
    public async Task WithoutALivestream_TheTierIsRefused()
    {
        var venue = await PublishedShowAsync(goOnline: true, withLivestream: false);

        var res = await AddTierAsync(venue, "Livestream", "Xem online");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "a stream ticket with no stream delivers nothing");
        (await res.Content.ReadAsStringAsync()).Should().Contain("livestream");
    }
}
