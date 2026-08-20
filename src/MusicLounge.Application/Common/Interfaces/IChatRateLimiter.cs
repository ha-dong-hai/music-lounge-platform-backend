namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// Per-user livestream chat rate limit (§6.10 — 1 message / 2 seconds, in-process, no Redis).
/// Documented in the design decisions but never actually wired into SendChatMessageCommandHandler
/// before this audit — any user could spam unlimited chat messages.
/// </summary>
public interface IChatRateLimiter
{
    /// <summary>Returns true if the user may send now, false if they must wait.</summary>
    bool TryAcquire(int userId);
}
