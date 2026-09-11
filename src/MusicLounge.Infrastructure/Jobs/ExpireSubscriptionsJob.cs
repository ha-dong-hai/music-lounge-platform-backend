using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

public sealed class ExpireSubscriptionsJob
{
    private readonly ApplicationDbContext _ctx;

    public ExpireSubscriptionsJob(ApplicationDbContext ctx) => _ctx = ctx;

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        // Combining the Status equality with the ExpiresAt comparison in one Where doesn't
        // translate under the SQLite provider used in tests — filter by Status server-side,
        // the date client-side (same limitation documented throughout this codebase).
        var due = (await _ctx.OwnerSubscriptions
                .Where(s => s.Status == SubscriptionStatus.Active)
                .ToListAsync(ct))
            .Where(s => s.ExpiresAt <= now)
            .ToList();

        if (due.Count == 0) return;

        foreach (var sub in due)
            // MLACP-371: goi chu da huy ket thuc voi trang thai Cancelled — phan biet voi goi het han tu nhien.
            sub.Status = sub.CancelledAt is null ? SubscriptionStatus.Expired : SubscriptionStatus.Cancelled;

        await _ctx.SaveChangesAsync(ct);
    }
}
