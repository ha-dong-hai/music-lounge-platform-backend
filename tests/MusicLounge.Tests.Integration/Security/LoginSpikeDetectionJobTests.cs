using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Security;

/// <summary>
/// Detects the same source IP failing login across many DIFFERENT accounts in a short window
/// (credential stuffing) — a pattern IAuthAttemptTracker's per-account lockout can't see.
/// Alert-only, with a 1-hour per-IP cooldown so an ongoing attack doesn't spam admins every run.
/// </summary>
[Collection("Integration")]
public sealed class LoginSpikeDetectionJobTests
{
    private readonly ApiFactory _factory;

    public LoginSpikeDetectionJobTests(ApiFactory factory) => _factory = factory;

    private async Task SeedFailuresAsync(ApplicationDbContext db, string ip, int count, DateTimeOffset when)
    {
        for (var i = 0; i < count; i++)
        {
            db.LoginFailureLogs.Add(new LoginFailureLog
            {
                Email = $"victim{i}-{Guid.NewGuid():N}@test.com",
                IpAddress = ip,
                CreatedAt = when
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ExecuteAsync_TenFailuresAcrossFiveAccounts_AlertsAllAdmins()
    {
        var ip = $"10.0.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await SeedFailuresAsync(db, ip, count: 10, when: now.AddMinutes(-2));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<LoginSpikeDetectionJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alert = await verifyDb.Notifications.FirstOrDefaultAsync(n =>
            n.UserId == SeedHelper.AdminId && n.Type == NotificationType.SecurityAlert && n.ReferenceId == ip);
        alert.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FewFailuresBelowThreshold_DoesNotAlert()
    {
        var ip = $"10.1.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // 3 accounts only — below MinDistinctAccounts (5), must not trigger.
            await SeedFailuresAsync(db, ip, count: 3, when: now.AddMinutes(-2));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<LoginSpikeDetectionJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alert = await verifyDb.Notifications.FirstOrDefaultAsync(n =>
            n.Type == NotificationType.SecurityAlert && n.ReferenceId == ip);
        alert.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_OutsideDetectionWindow_IsIgnored()
    {
        var ip = $"10.2.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // 10 minutes is the window boundary — 30 minutes ago is well outside it.
            await SeedFailuresAsync(db, ip, count: 10, when: now.AddMinutes(-30));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<LoginSpikeDetectionJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alert = await verifyDb.Notifications.FirstOrDefaultAsync(n =>
            n.Type == NotificationType.SecurityAlert && n.ReferenceId == ip);
        alert.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_CalledTwiceWithinCooldown_AlertsOnlyOnce()
    {
        var ip = $"10.3.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await SeedFailuresAsync(db, ip, count: 10, when: now.AddMinutes(-2));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<LoginSpikeDetectionJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        // Same attack still ongoing — seed a second batch of fresh failures from the same IP.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await SeedFailuresAsync(db, ip, count: 10, when: DateTimeOffset.UtcNow.AddMinutes(-1));
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<LoginSpikeDetectionJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alertCount = await verifyDb.Notifications.CountAsync(n =>
            n.UserId == SeedHelper.AdminId && n.Type == NotificationType.SecurityAlert && n.ReferenceId == ip);
        alertCount.Should().Be(1, "the 1-hour cooldown must prevent a second alert for the same ongoing attack");
    }

    [Fact]
    public async Task ExecuteAsync_PrunesFailureLogsOlderThanRetention()
    {
        var ip = $"10.4.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        var staleId = 0;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stale = new LoginFailureLog
            {
                Email = "old@test.com",
                IpAddress = ip,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-25) // retention is 24h
            };
            db.LoginFailureLogs.Add(stale);
            await db.SaveChangesAsync();
            staleId = stale.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<LoginSpikeDetectionJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stillThere = await verifyDb.LoginFailureLogs.FindAsync(staleId);
        stillThere.Should().BeNull();
    }
}
