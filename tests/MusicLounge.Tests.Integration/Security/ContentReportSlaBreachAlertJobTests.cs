using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Security;

/// <summary>
/// Regression test for a bug found during the 2026-09-07 full-backend review: ExecuteAsync built
/// the breach alert and called INotificationService.NotifyAsync (which only stages via Add(), see
/// NotificationService's own comment), but never called SaveChangesAsync — so no Notification row
/// was ever actually persisted. Fixed by adding the missing SaveChangesAsync at the end of the job.
/// </summary>
[Collection("Integration")]
public sealed class ContentReportSlaBreachAlertJobTests
{
    private readonly ApiFactory _factory;

    public ContentReportSlaBreachAlertJobTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ExecuteAsync_ReportOverdueBeyondSlaHours_PersistsAdminNotification()
    {
        var targetId = Random.Shared.Next(100_000, 999_999);
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ContentReports.Add(new ContentReport
            {
                TargetType = ReportTargetType.Show,
                TargetId = targetId,
                ReporterId = SeedHelper.AudienceId,
                Reason = "test",
                Status = ContentReportStatus.Open,
                // default SLA is 48h — 50h ago is safely past it.
                CreatedAt = now.AddHours(-50)
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ContentReportSlaBreachAlertJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alert = await verifyDb.Notifications.FirstOrDefaultAsync(n =>
            n.UserId == SeedHelper.AdminId
            && n.Type == NotificationType.ContentReportSlaBreached
            && n.ReferenceId == $"Show:{targetId}");
        alert.Should().NotBeNull("the fix must make ExecuteAsync commit the staged Notification via SaveChangesAsync");
    }

    [Fact]
    public async Task ExecuteAsync_ReportWithinSlaHours_DoesNotAlert()
    {
        var targetId = Random.Shared.Next(100_000, 999_999);
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ContentReports.Add(new ContentReport
            {
                TargetType = ReportTargetType.Show,
                TargetId = targetId,
                ReporterId = SeedHelper.AudienceId,
                Reason = "test",
                Status = ContentReportStatus.Open,
                // well within the default 48h SLA.
                CreatedAt = now.AddHours(-2)
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ContentReportSlaBreachAlertJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alert = await verifyDb.Notifications.FirstOrDefaultAsync(n =>
            n.Type == NotificationType.ContentReportSlaBreached && n.ReferenceId == $"Show:{targetId}");
        alert.Should().BeNull();
    }
}
