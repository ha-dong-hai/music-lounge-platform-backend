using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Exceptions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Hubs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Fakes;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-643. Đo 05/10/2026 với 6 người xem thật: (1) chủ phòng trà tắt khung chat thì máy chủ chặn đúng, nhưng người xem
/// không được báo — ô nhập vẫn mở, gõ xong mới nhận câu lỗi chung; (2) mọi lỗi khi gửi tin (gửi quá nhanh, chat đã tắt)
/// tới người gửi thành "Không gửi được tin nhắn, thử lại." vì SignalR giấu nội dung ngoại lệ không phải HubException.
/// </summary>
[Collection("Integration")]
public sealed class ChatStateAndErrorTests
{
    private readonly ApiFactory _factory;

    public ChatStateAndErrorTests(ApiFactory factory) => _factory = factory;

    private RecordingLivestreamHubService Hub =>
        (RecordingLivestreamHubService)_factory.Services.GetRequiredService<ILivestreamHubService>();

    private async Task<(Guid OwnerId, Guid LivestreamId)> LiveStreamAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"chat643-{Guid.NewGuid():N}@test.com", FullName = "Chu 643", Role = UserRole.Owner };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue643-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        var start = DateTimeOffset.UtcNow.AddMinutes(-30);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Show643-{Guid.NewGuid():N}", Format = LoungeShowFormat.Online,
            Status = LoungeShowStatus.Ongoing, ScheduledStart = start, ScheduledEnd = start.AddHours(2)
        };
        db.Add(show);
        await db.SaveChangesAsync();
        var livestream = new Livestream { LoungeShowId = show.Id, Status = LivestreamStatus.Live, StartedAt = start, ChatEnabled = true };
        db.Add(livestream);
        await db.SaveChangesAsync();
        return (owner.Id, livestream.Id);
    }

    [Fact]
    public async Task TurningTheChatOffAndOn_IsAnnouncedToEveryoneWatching()
    {
        var (ownerId, livestreamId) = await LiveStreamAsync();
        var owner = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        (await owner.PostAsJsonAsync($"/api/v1/livestreams/{livestreamId}/chat-enabled", new { Enabled = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.PostAsJsonAsync($"/api/v1/livestreams/{livestreamId}/chat-enabled", new { Enabled = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        Hub.For(livestreamId).Where(s => s.Event == "ChatEnabledChanged").Select(s => (bool)s.Payload!)
            .Should().Equal([false, true], "viewers must see the chat box close and reopen without reloading the page");
    }

    [Fact]
    public void ABusinessRuleRefusal_ReachesTheSender_InItsOwnWords()
    {
        LivestreamHubErrors.ViewerMessage(new DomainException("Chat đã bị tắt cho livestream này."))
            .Should().Be("Chat đã bị tắt cho livestream này.");
        LivestreamHubErrors.ViewerMessage(new ValidationException(new Dictionary<string, string[]>
            {
                ["Message"] = ["Tin nhắn không vượt quá 500 ký tự."]
            }))
            .Should().Be("Tin nhắn không vượt quá 500 ký tự.", "the field message, not the generic 'invalid data'");
    }

    [Fact]
    public void ASystemFailure_IsNotPassedOnToTheViewer()
    {
        LivestreamHubErrors.ViewerMessage(new InvalidOperationException("Connection string 'X' timed out"))
            .Should().BeNull("internal details must not reach the browser");
    }

    [Fact]
    public async Task TheHubThrowsAHubException_SoSignalRDoesNotHideTheSentence()
    {
        var act = () => LivestreamHubErrors.RunAsync(
            () => throw new DomainException("Bạn đang gửi tin nhắn quá nhanh. Vui lòng đợi vài giây rồi thử lại."));

        (await act.Should().ThrowAsync<HubException>()).Which.Message
            .Should().Be("Bạn đang gửi tin nhắn quá nhanh. Vui lòng đợi vài giây rồi thử lại.");
    }
}
