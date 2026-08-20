using FluentAssertions;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// CF4 §6.10 — chat rate limit (1 message / 2 seconds per user). SendChatMessageCommand is only
/// reachable through the SignalR hub (LivestreamHub.SendMessage), not a REST endpoint, and this
/// test project has no SignalR client harness — testing the rate limiter itself directly against
/// the real DI-registered singleton is the proportionate way to verify it without building a whole
/// new SignalR test infrastructure for one small, pure, already-isolated component.
/// </summary>
[Collection("Integration")]
public sealed class ChatRateLimiterTests
{
    private readonly ApiFactory _factory;

    public ChatRateLimiterTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void TryAcquire_SecondCallWithinTwoSeconds_IsRejected()
    {
        var limiter = _factory.Services.GetRequiredService<IChatRateLimiter>();
        var userId = Random.Shared.Next(1_000_000, 2_000_000); // isolated from any seeded/real user id

        limiter.TryAcquire(userId).Should().BeTrue("first message for this user should always be allowed");
        limiter.TryAcquire(userId).Should().BeFalse("second message within 2 seconds must be rejected");
    }

    [Fact]
    public void TryAcquire_DifferentUsers_DoNotRateLimitEachOther()
    {
        var limiter = _factory.Services.GetRequiredService<IChatRateLimiter>();
        var userA = Random.Shared.Next(2_000_000, 3_000_000);
        var userB = Random.Shared.Next(3_000_000, 4_000_000);

        limiter.TryAcquire(userA).Should().BeTrue();
        limiter.TryAcquire(userB).Should().BeTrue("rate limiting is per-user, not global");
    }

    [Fact]
    public async Task TryAcquire_AfterIntervalElapses_IsAllowedAgain()
    {
        var limiter = _factory.Services.GetRequiredService<IChatRateLimiter>();
        var userId = Random.Shared.Next(4_000_000, 5_000_000);

        limiter.TryAcquire(userId).Should().BeTrue();
        await Task.Delay(TimeSpan.FromSeconds(2.1));
        limiter.TryAcquire(userId).Should().BeTrue("2+ seconds have passed since the last message");
    }
}
