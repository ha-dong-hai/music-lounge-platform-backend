using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-305. NotificationService only calls Add() — its contract says the caller saves — and
/// TransactionBehavior does not save either: it begins and commits, and CommitTransactionAsync
/// commits the database transaction without flushing the change tracker.
///
/// Five command handlers notified after their last save, so the row was staged and then vanished.
/// Nothing failed. The notification simply never appeared, which is why none of this was noticed:
/// no test covered any of them.
///
/// The same mistake had already happened once, in ReviewKycDocumentCommandHandler (MLACP-290),
/// where a test caught it immediately. That is the whole difference — these tests are the guard.
/// </summary>
[Collection("Integration")]
public sealed class NotificationsActuallyPersistTests
{
    private readonly ApiFactory _factory;

    public NotificationsActuallyPersistTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<int> NotificationCountAsync(int userId, NotificationType type)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n => n.UserId == userId && n.Type == type);
    }

    private async Task<(int LoungeId, int OwnerId)> SeedVenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User
        {
            Email = $"notify-{Guid.NewGuid():N}@test.com",
            FullName = "Chủ Phòng Trà",
            Role = UserRole.Owner,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLounge.Domain.Entities.MusicLounge
        {
            OwnerId = owner.Id,
            Name = $"NotifyVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, owner.Id);
    }

    [Fact]
    public async Task AVenueToldItWasPenalised_ActuallyReceivesTheNotification()
    {
        // Being penalised without being told is the worst version of this bug: the venue's status
        // changes underneath them and the only message explaining why never arrives.
        var (loungeId, ownerId) = await SeedVenueAsync();
        var before = await NotificationCountAsync(ownerId, NotificationType.PenaltyIssued);

        var res = await Admin().PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = loungeId,
            PenaltyType = "Warning",
            Reason = "Nội dung quảng cáo sai sự thật",
            EvidenceRef = (string?)null,
            SuspensionDays = (int?)null
        });
        res.IsSuccessStatusCode.Should().BeTrue();

        (await NotificationCountAsync(ownerId, NotificationType.PenaltyIssued))
            .Should().Be(before + 1, "the row was staged before this fix and never written");
    }

    [Fact]
    public async Task AppealingAPenalty_NotifiesTheAdmins()
    {
        var (loungeId, ownerId) = await SeedVenueAsync();

        var issued = await Admin().PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = loungeId,
            PenaltyType = "Suspension",
            Reason = "Vi phạm lặp lại",
            EvidenceRef = (string?)null,
            SuspensionDays = 7
        });
        issued.IsSuccessStatusCode.Should().BeTrue();

        int penaltyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            penaltyId = (await db.Set<VenuePenalty>()
                .Where(p => p.LoungeId == loungeId).ToListAsync()).Single().Id;
        }

        var before = await NotificationCountAsync(SeedHelper.AdminId, NotificationType.PenaltyIssued);

        var appeal = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .PostAsJsonAsync($"/api/v1/venue-penalties/{penaltyId}/appeal",
                new { AppealReason = "Chúng tôi đã gỡ nội dung ngay khi nhận được phản ánh" });
        appeal.IsSuccessStatusCode.Should().BeTrue();

        (await NotificationCountAsync(SeedHelper.AdminId, NotificationType.PenaltyIssued))
            .Should().BeGreaterThan(before,
                "an appeal nobody is told about is an appeal nobody reviews");
    }

    [Fact]
    public async Task ResolvingAnAppeal_NotifiesTheVenue()
    {
        var (loungeId, ownerId) = await SeedVenueAsync();

        await Admin().PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = loungeId,
            PenaltyType = "Suspension",
            Reason = "Vi phạm nội dung",
            EvidenceRef = (string?)null,
            SuspensionDays = 7
        });

        int penaltyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            penaltyId = (await db.Set<VenuePenalty>()
                .Where(p => p.LoungeId == loungeId).ToListAsync()).Single().Id;
        }

        await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .PostAsJsonAsync($"/api/v1/venue-penalties/{penaltyId}/appeal",
                new { AppealReason = "Xin xem xét lại" });

        var before = await NotificationCountAsync(ownerId, NotificationType.AppealResolved);

        var review = await Admin().PostAsJsonAsync(
            $"/api/v1/venue-penalties/{penaltyId}/appeal/review",
            new { Decision = "Upheld", ReviewNote = "Bằng chứng chưa đủ thuyết phục" });
        review.IsSuccessStatusCode.Should().BeTrue();

        (await NotificationCountAsync(ownerId, NotificationType.AppealResolved))
            .Should().Be(before + 1,
                "the outcome of an appeal is the one thing the venue is waiting for");
    }
}
