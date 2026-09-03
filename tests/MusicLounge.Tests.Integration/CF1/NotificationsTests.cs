using System.Net;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// W23 — Notification inbox
/// GET /api/v1/notifications | POST /api/v1/notifications/{id}/read
/// </summary>
[Collection("Integration")]
public sealed class NotificationsTests
{
    private readonly ApiFactory _factory;

    public NotificationsTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedNotificationAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var notif = new Notification
        {
            UserId = userId,
            Type = NotificationType.TicketConfirmed,
            Title = "Test notification",
            Body = "Test body",
            ReferenceType = "ticket",
            ReferenceId = Guid.NewGuid().ToString(),
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(notif);
        await db.SaveChangesAsync();
        return notif.Id;
    }

    [Fact]
    public async Task GetMyNotifications_ReturnsOwnOnly()
    {
        await SeedNotificationAsync(SeedHelper.AudienceId);
        await SeedNotificationAsync(SeedHelper.OtherOwnerId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync("/api/v1/notifications");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Test notification");
    }

    [Fact]
    public async Task MarkRead_ByOwner_SetsIsReadTrue()
    {
        var notifId = await SeedNotificationAsync(SeedHelper.AudienceId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsync($"/api/v1/notifications/{notifId}/read", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notif = await db.Notifications.FindAsync(notifId);
        notif!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkRead_ByNonOwner_Returns403()
    {
        var notifId = await SeedNotificationAsync(SeedHelper.AudienceId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PostAsync($"/api/v1/notifications/{notifId}/read", null);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkAllRead_SetsAllOwnUnreadToTrue_LeavesOthersUntouched()
    {
        var ownUnreadId1 = await SeedNotificationAsync(SeedHelper.AudienceId);
        var ownUnreadId2 = await SeedNotificationAsync(SeedHelper.AudienceId);
        var otherUserUnreadId = await SeedNotificationAsync(SeedHelper.OtherOwnerId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsync("/api/v1/notifications/read-all", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.FindAsync(ownUnreadId1))!.IsRead.Should().BeTrue();
        (await db.Notifications.FindAsync(ownUnreadId2))!.IsRead.Should().BeTrue();
        (await db.Notifications.FindAsync(otherUserUnreadId))!.IsRead.Should().BeFalse();
    }
}
