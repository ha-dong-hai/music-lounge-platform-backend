using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

public sealed class CancelAbandonedPaymentsJob
{
    private readonly ApplicationDbContext _ctx;

    public CancelAbandonedPaymentsJob(ApplicationDbContext ctx) => _ctx = ctx;

    // VNPay retries the callback for ~15 minutes. We wait 30 minutes before declaring a payment abandoned.
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);

        // Combining the Status equality with the CreatedAt comparison in one Where doesn't
        // translate under the SQLite provider used in tests (same limitation documented
        // repeatedly elsewhere in this codebase, empirically confirmed here by a test that used
        // to throw) — filter by Status server-side, the date client-side.
        var stalePaymentIds = (await _ctx.Payments
                .Where(p => p.Status == PaymentStatus.Pending)
                .Select(p => new { p.Id, p.CreatedAt })
                .ToListAsync(ct))
            .Where(p => p.CreatedAt <= cutoff)
            .Select(p => p.Id)
            .ToList();

        if (stalePaymentIds.Count == 0) return;

        await _ctx.Tickets
            .Where(t => t.PaymentId.HasValue
                && stalePaymentIds.Contains(t.PaymentId.Value)
                && t.Status == TicketStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Cancelled), ct);

        // A second empirically-confirmed SQLite provider limitation (distinct from the one above):
        // ExecuteUpdateAsync's SetProperty translator chokes on the implicit DateTimeOffset ->
        // DateTimeOffset? conversion needed to assign UtcNow into the nullable UpdatedAt column.
        // Falls back to fetch-then-mutate — the batch here is inherently small (payments stuck
        // >30 minutes), so losing the single-statement bulk UPDATE is not a real cost.
        var stalePayments = await _ctx.Payments
            .Where(p => stalePaymentIds.Contains(p.Id))
            .ToListAsync(ct);
        foreach (var payment in stalePayments)
        {
            payment.Status = PaymentStatus.Failed;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _ctx.SaveChangesAsync(ct);
    }
}
