using System.Collections.Concurrent;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

// Registered as Singleton so the semaphore dictionary is shared by every request in the process —
// see IShowBookingLock for the same pattern already used for show-quota locking. This one is
// keyed by an arbitrary string so it can guard other check-then-act sections (VNPay callback
// idempotency, ticket purchase/cancel idempotency) without a semaphore dictionary per use case.
internal sealed class AsyncKeyedLock : IAsyncKeyedLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct = default)
    {
        var gate = Locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
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
