using System.Collections.Concurrent;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// In-process implementation — registered as a Singleton so the dictionary is shared process-wide
/// (same reasoning as ShowBookingLock: a Scoped/Transient registration would give each request its
/// own empty dictionary and rate-limit nothing). Bounded by active user count, not request volume,
/// so no eviction is needed at this scale.
/// </summary>
internal sealed class ChatRateLimiter : IChatRateLimiter
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(2);
    private readonly ConcurrentDictionary<int, DateTimeOffset> _lastSentAt = new();

    public bool TryAcquire(int userId)
    {
        var now = DateTimeOffset.UtcNow;
        var recorded = _lastSentAt.AddOrUpdate(
            userId,
            now,
            (_, last) => now - last >= MinInterval ? now : last);
        return recorded == now;
    }
}
