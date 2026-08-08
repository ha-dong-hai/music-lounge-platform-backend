using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

public sealed class ReleaseExpiredHoldsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly IAsyncKeyedLock _lock;

    public ReleaseExpiredHoldsJob(ApplicationDbContext ctx, IAsyncKeyedLock @lock)
    {
        _ctx = ctx;
        _lock = @lock;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var expiredIds = await _ctx.TicketHolds
            .Where(h => h.ExpiresAt <= DateTimeOffset.UtcNow)
            .Select(h => h.Id)
            .ToListAsync(ct);

        // One hold at a time, each under the SAME lock key PurchaseTicketCommandHandler acquires
        // for that hold — without this, a hold expiring at the exact moment a Purchase for it is
        // mid-flight gets deleted out from under the purchase's still-open transaction, which
        // fails as a raw DbUpdateConcurrencyException (0 rows affected) instead of a clean
        // business error, and rolls back the Payment/Tickets that were about to be created.
        foreach (var holdId in expiredIds)
        {
            await using var _ = await _lock.AcquireAsync($"purchase-hold:{holdId}", ct);

            var hold = await _ctx.TicketHolds.FindAsync([holdId], ct);
            if (hold is null || hold.ExpiresAt > DateTimeOffset.UtcNow) continue;

            _ctx.TicketHolds.Remove(hold);
            await _ctx.SaveChangesAsync(ct);
        }
    }
}
