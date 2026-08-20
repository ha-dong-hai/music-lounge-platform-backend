using System.Collections.Concurrent;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// In-process per-show mutex. Registered as a Singleton so the semaphore dictionary is shared by
/// every request in the process — a Scoped/Transient registration would create a fresh, useless
/// semaphore per request and provide no protection at all.
///
/// Closes the check-then-act race in ticket quota validation (BR-14): without this, two requests
/// for the same show can both read "1 seat left" and both insert, overselling the show. This is
/// a single-process lock — if the API is ever scaled to more than one instance behind a load
/// balancer, upgrade to a database-level lock (SQL Server sp_getapplock keyed by ShowId, or a
/// distributed lock) since two different processes do not share this dictionary.
/// </summary>
internal sealed class ShowBookingLock : IShowBookingLock
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    public async Task<IAsyncDisposable> AcquireAsync(int showId, CancellationToken ct = default)
    {
        var gate = Locks.GetOrAdd(showId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
