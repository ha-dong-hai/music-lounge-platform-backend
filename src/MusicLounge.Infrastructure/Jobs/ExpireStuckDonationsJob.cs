using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// Cancels donations stuck in PendingPayment after 2h — covers the case where the user
/// abandoned the VNPay page and the callback never arrived.
/// </summary>
public sealed class ExpireStuckDonationsJob
{
    private readonly ApplicationDbContext _ctx;

    public ExpireStuckDonationsJob(ApplicationDbContext ctx) => _ctx = ctx;

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var cutoff = DateTimeOffset.UtcNow.AddHours(-2);

        // Combining the Status equality with the CreatedAt comparison in one Where doesn't
        // translate under the SQLite provider used in tests — filter by Status server-side,
        // the date client-side (same limitation documented throughout this codebase).
        var stuck = (await _ctx.Donations
                .Where(d => d.Status == DonationStatus.PendingPayment)
                .ToListAsync(ct))
            .Where(d => d.CreatedAt <= cutoff)
            .ToList();

        if (stuck.Count == 0) return;

        foreach (var donation in stuck)
            donation.Status = DonationStatus.Cancelled;

        await _ctx.SaveChangesAsync(ct);
    }
}
