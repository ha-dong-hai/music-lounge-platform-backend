using System.Collections.Concurrent;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// In-process per-show mutex. The semaphore dictionary is static, so it is shared by every request in the process.
///
/// Closes the check-then-act race in ticket quota validation (BR-14): without this, two requests for the same show can
/// both read "1 seat left" and both insert, overselling the show. This is a single-process lock — if the API is ever
/// scaled to more than one instance behind a load balancer, upgrade to a database-level lock (SQL Server sp_getapplock
/// keyed by ShowId, or a distributed lock) since two different processes do not share this dictionary.
///
/// MLACP-396: registered Scoped (was Singleton) so it can see the request's <see cref="TransactionLockScope"/>. Inside a
/// command's transaction the lock is held until that transaction has committed or rolled back — released when the
/// handler returned (the old behaviour), the second request could read the quota before the first request's tickets
/// were committed, which is the very oversell this lock exists to stop.
/// </summary>
internal sealed class ShowBookingLock : IShowBookingLock
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    private readonly TransactionLockScope _scope;

    public ShowBookingLock(TransactionLockScope scope) => _scope = scope;

    public async Task<IAsyncDisposable> AcquireAsync(int showId, CancellationToken ct = default)
    {
        var scopedKey = "show-booking:" + showId;
        if (_scope.Holds(scopedKey)) return TransactionLockScope.AlreadyHeld;

        var gate = Locks.GetOrAdd(showId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return _scope.Adopt(scopedKey, new Releaser(gate));
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
