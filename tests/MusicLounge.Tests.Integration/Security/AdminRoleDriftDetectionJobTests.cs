using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Security;

/// <summary>
/// There is no API path to promote a user to Admin — every Admin account today only exists via a
/// direct database edit. This job compares the current Role=Admin set against a persisted baseline
/// (KnownAdminSnapshot) so an unexpected promotion gets flagged regardless of how it happened.
/// </summary>
[Collection("Integration")]
public sealed class AdminRoleDriftDetectionJobTests
{
    private readonly ApiFactory _factory;

    public AdminRoleDriftDetectionJobTests(ApiFactory factory) => _factory = factory;

    private static User NewUser(string email, UserRole role) => new()
    {
        Email = email,
        FullName = "Drift Test User",
        Role = role,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task ExecuteAsync_FirstRunWithEmptyBaseline_BootstrapsWithoutAlerting()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Force the "never run before" precondition regardless of what earlier tests left behind.
            db.KnownAdminSnapshots.RemoveRange(db.KnownAdminSnapshots);
            await db.SaveChangesAsync();
        }

        int notificationsBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            notificationsBefore = await db.Notifications.CountAsync(n => n.Type == NotificationType.SecurityAlert);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<AdminRoleDriftDetectionJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var snapshot = await verifyDb.KnownAdminSnapshots.FirstOrDefaultAsync(s => s.UserId == SeedHelper.AdminId);
        snapshot.Should().NotBeNull("the pre-existing seed Admin must be silently baselined, not treated as new");

        var notificationsAfter = await verifyDb.Notifications.CountAsync(n => n.Type == NotificationType.SecurityAlert);
        notificationsAfter.Should().Be(notificationsBefore, "a first-ever run must never alert on admins that already legitimately existed");
    }

    [Fact]
    public async Task ExecuteAsync_NewAdminAppears_AlertsExistingAdminsAndUpdatesBaseline()
    {
        int newAdminId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Guarantee the baseline already covers every currently-real admin, so this run's diff
            // is driven only by the one new admin this test introduces below.
            var currentAdminIds = await db.Users.Where(u => u.Role == UserRole.Admin)
                .Select(u => u.Id).ToListAsync();
            var knownIds = await db.KnownAdminSnapshots.Select(s => s.UserId).ToListAsync();
            foreach (var id in currentAdminIds.Except(knownIds))
                db.KnownAdminSnapshots.Add(new KnownAdminSnapshot { UserId = id, FirstDetectedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var newAdmin = NewUser($"drift-admin-{Guid.NewGuid():N}@test.com", UserRole.Admin);
            db.Users.Add(newAdmin);
            await db.SaveChangesAsync();
            newAdminId = newAdmin.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<AdminRoleDriftDetectionJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var alert = await verifyDb.Notifications.FirstOrDefaultAsync(n =>
                n.UserId == SeedHelper.AdminId
                && n.Type == NotificationType.SecurityAlert
                && n.ReferenceId == newAdminId.ToString());
            alert.Should().NotBeNull();

            var snapshot = await verifyDb.KnownAdminSnapshots.FirstOrDefaultAsync(s => s.UserId == newAdminId);
            snapshot.Should().NotBeNull();
        }

        // Demotion path: once no longer Admin, the baseline entry must be dropped so a FUTURE
        // re-promotion of this same account is caught again rather than silently trusted forever.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == newAdminId);
            user.Role = UserRole.Audience;
            await db.SaveChangesAsync();
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<AdminRoleDriftDetectionJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var remainingSnapshot = await finalDb.KnownAdminSnapshots.FirstOrDefaultAsync(s => s.UserId == newAdminId);
        remainingSnapshot.Should().BeNull("a demoted admin must be removed from the baseline");
    }
}
