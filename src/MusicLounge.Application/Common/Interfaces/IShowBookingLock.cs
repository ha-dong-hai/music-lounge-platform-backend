namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// Serializes ticket-quota check-then-reserve operations for the same show so two concurrent
/// buyers cannot both pass the availability check before either commits (oversell/double-booking).
/// Scoped to a single process — see <c>ShowBookingLock</c> in Infrastructure for the horizontal-
/// scaling caveat if the API is ever deployed as more than one instance behind a load balancer.
/// </summary>
public interface IShowBookingLock
{
    Task<IAsyncDisposable> AcquireAsync(int showId, CancellationToken ct = default);
}
