using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// The one place that decides whether a LoungeShow may still change state.
///
/// LoungeShowStatus is written from 13 sites across 7 feature folders. The five inside the
/// LoungeShows folder each guard their own source status before transitioning, but the four
/// livestream-driven ones (StartLivestream, EndLivestream, TerminateLivestream, the Mux idle
/// webhook, and LivestreamReconnectTimeoutJob) only ever guarded <c>livestream.Status</c> and wrote
/// <c>show.Status</c> unconditionally. CancelLoungeShow deliberately allows cancelling a show whose
/// livestream is merely Scheduled or Reconnecting, so this sequence was reachable:
///
///   livestream disconnects (Status -> Reconnecting, timeout job scheduled)
///   -> Owner cancels the show (allowed: not Live) -> tickets cancelled, 100% refunds requested
///   -> timeout job fires 5 minutes later -> show.Status = Ended, ActualEnd and RatingOpenUntil set
///
/// leaving a cancelled, fully-refunded show reading as completed: counted in owner analytics, open
/// for audience ratings, and feeding the completed-show count that decides a venue's settlement
/// payout tier.
/// </summary>
public static class LoungeShowLifecycle
{
    /// <summary>Cancelled and Ended are absorbing states — nothing may move a show out of them.</summary>
    public static bool IsTerminal(LoungeShowStatus status)
        => status is LoungeShowStatus.Cancelled or LoungeShowStatus.Ended;

    /// <summary>
    /// Applies the "show is over" transition, unless the show already reached a terminal state.
    /// Returns false when it declined, so webhook and background-job callers can log and carry on
    /// finishing the livestream side without failing — a Mux webhook that throws is retried by Mux
    /// indefinitely, which is worse than a no-op.
    /// </summary>
    public static bool TryMarkEnded(LoungeShow show, DateTimeOffset now, int ratingWindowDays)
    {
        if (IsTerminal(show.Status)) return false;

        show.Status = LoungeShowStatus.Ended;
        show.ActualEnd = now;
        show.RatingOpenUntil = now.AddDays(ratingWindowDays);   // §6.13
        return true;
    }
}
