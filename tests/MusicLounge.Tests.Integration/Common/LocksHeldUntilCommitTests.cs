using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Common;

/// <summary>
/// MLACP-396. Khoá chống tranh chấp phải được giữ cho tới khi transaction của command commit (hoặc rollback) — không phải
/// tới lúc handler trả về. <see cref="TransactionCommitProbe"/> kiểm tra đúng lúc sắp commit xem một request khác có lấy
/// được khoá không: trước task này lấy được, nên request thứ hai đọc được dữ liệu chưa commit của request thứ nhất.
/// </summary>
[Collection("Integration")]
public sealed class LocksHeldUntilCommitTests
{
    private readonly ApiFactory _factory;

    public LocksHeldUntilCommitTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int ShowId, int PriceId);

    private async Task<Venue> PublishedShowAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"lock396-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue396-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18], Description = "MLACP-396",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        var tier = new TicketTier { LoungeShowId = show.Id, Name = "Vào cửa", AccessType = AccessType.Physical, TotalCapacity = 50 };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = 100_000m, Quota = 50, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(2),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();
        return new Venue(owner.Id, lounge.Id, show.Id, price.Id);
    }

    private HttpClient Owner(Venue venue) => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);

    /// <summary>Từ một scope khác, thử lấy khoá trong 250ms: true nghĩa là khoá đang bị giữ.</summary>
    private async Task<bool> KeyedLockIsHeldAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var locks = scope.ServiceProvider.GetRequiredService<IAsyncKeyedLock>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        try
        {
            await using var _ = await locks.AcquireAsync(key, cts.Token);
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    private async Task<bool> ShowBookingLockIsHeldAsync(int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var locks = scope.ServiceProvider.GetRequiredService<IShowBookingLock>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        try
        {
            await using var _ = await locks.AcquireAsync(showId, cts.Token);
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>Chạy <paramref name="act"/> và ghi lại, ở mỗi lần có transaction sắp commit, khoá có đang bị giữ không.</summary>
    private async Task<List<bool>> HeldAtEachCommitAsync(Func<Task<bool>> isHeld, Func<Task> act)
    {
        var probe = _factory.Services.GetRequiredService<TransactionCommitProbe>();
        var seen = new List<bool>();
        probe.Arm(async () => seen.Add(await isHeld()));
        try
        {
            await act();
        }
        finally
        {
            probe.Disarm();
        }
        return seen;
    }

    [Fact]
    public async Task AKeyedLock_IsStillHeldWhenTheCommandCommits()
    {
        var venue = await PublishedShowAsync();

        var seen = await HeldAtEachCommitAsync(
            () => KeyedLockIsHeldAsync($"show-status-change:{venue.ShowId}"),
            async () => (await Owner(venue).PostAsync($"/api/v1/lounge-shows/{venue.ShowId}/cancel", null))
                .StatusCode.Should().Be(HttpStatusCode.NoContent));

        seen.Should().NotBeEmpty();
        seen.Last().Should().BeTrue("released before the commit, a second request could read the not-yet-cancelled show");
    }

    [Fact]
    public async Task TheShowBookingLock_IsStillHeldWhenTheHoldCommits()
    {
        var venue = await PublishedShowAsync();

        var seen = await HeldAtEachCommitAsync(
            () => ShowBookingLockIsHeldAsync(venue.ShowId),
            async () => (await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
                    .PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = venue.PriceId, Quantity = 1 }))
                .StatusCode.Should().Be(HttpStatusCode.Created));

        seen.Should().NotBeEmpty();
        seen.Last().Should().BeTrue("otherwise two holds for the last seat can both pass the quota check");
    }

    [Fact]
    public async Task TheLock_IsReleasedOnceTheCommandHasCommitted()
    {
        var venue = await PublishedShowAsync();

        (await Owner(venue).PostAsync($"/api/v1/lounge-shows/{venue.ShowId}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await KeyedLockIsHeldAsync($"show-status-change:{venue.ShowId}"))
            .Should().BeFalse("a lock kept after the commit would block every later request on this show");
    }

    [Fact]
    public async Task TheLock_IsReleasedWhenTheCommandFails()
    {
        var venue = await PublishedShowAsync();
        (await Owner(venue).PostAsync($"/api/v1/lounge-shows/{venue.ShowId}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Owner(venue).PostAsync($"/api/v1/lounge-shows/{venue.ShowId}/cancel", null))
            .IsSuccessStatusCode.Should().BeFalse("a cancelled show cannot be cancelled again");

        (await KeyedLockIsHeldAsync($"show-status-change:{venue.ShowId}"))
            .Should().BeFalse("a failed command rolls back and must still let go of its locks");
    }

    [Fact]
    public async Task TakingTheSameLockTwiceInOneTransaction_DoesNotWaitOnItself()
    {
        var key = $"reentrant-396:{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var transaction = scope.ServiceProvider.GetRequiredService<ITransactionLockScope>();
        var locks = scope.ServiceProvider.GetRequiredService<IAsyncKeyedLock>();

        transaction.Begin();
        await using (await locks.AcquireAsync(key)) { }
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            await using (await locks.AcquireAsync(key, cts.Token)) { }

        (await KeyedLockIsHeldAsync(key)).Should().BeTrue("the transaction still holds it after the handler disposed it");
        await transaction.EndAsync();
        (await KeyedLockIsHeldAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task OutsideATransaction_DisposingTheLockReleasesItAtOnce()
    {
        var key = $"job-396:{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
            await using (await scope.ServiceProvider.GetRequiredService<IAsyncKeyedLock>().AcquireAsync(key)) { }

        (await KeyedLockIsHeldAsync(key)).Should().BeFalse("a Hangfire job has no transaction to wait for");
    }
}
