using System.Collections.Concurrent;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

// The semaphore dictionary is STATIC, so every request in the process shares it — see IShowBookingLock for the same
// pattern already used for show-quota locking. This one is keyed by an arbitrary string so it can guard other
// check-then-act sections (VNPay callback idempotency, ticket purchase/cancel idempotency) without a semaphore
// dictionary per use case.
//
// MLACP-396: registered Scoped (was Singleton) so it can see the request's TransactionLockScope. Inside a command's
// transaction, disposing the handle does NOT release the lock — the scope releases it after TransactionBehavior has
// committed or rolled back. Releasing when the handler returned (the old behaviour) left a window before the commit in
// which a second request could take the lock and read the not-yet-committed state. Outside a transaction (Hangfire
// jobs) disposing releases immediately, as before. Still a single-process lock: several API instances would need a
// database-level lock (e.g. SQL Server sp_getapplock).
internal sealed class AsyncKeyedLock : IAsyncKeyedLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    private readonly TransactionLockScope _scope;

    public AsyncKeyedLock(TransactionLockScope scope) => _scope = scope;

    public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct = default)
    {
        var scopedKey = "keyed:" + key;
        if (_scope.Holds(scopedKey)) return TransactionLockScope.AlreadyHeld;

        var gate = Locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
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
