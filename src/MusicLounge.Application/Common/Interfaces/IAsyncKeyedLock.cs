namespace MusicLounge.Application.Common.Interfaces;

// In-process mutex keyed by an arbitrary string (payment order id, hold id, ticket id...). Same
// single-instance caveat as IShowBookingLock: only guards against concurrent requests within one
// process — upgrade to a DB-level lock if the API is ever scaled to more than one instance.
public interface IAsyncKeyedLock
{
    Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct = default);
}
