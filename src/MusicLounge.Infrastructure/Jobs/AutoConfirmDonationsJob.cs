using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// D4: Owner không xác nhận trong 24h → auto-confirm + flag auto_confirmed=true cho Admin biết.
/// </summary>
public sealed class AutoConfirmDonationsJob
{
    private readonly ApplicationDbContext _ctx;

    public AutoConfirmDonationsJob(ApplicationDbContext ctx) => _ctx = ctx;

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);

        // Use PaymentConfirmedAt (not CreatedAt) so the 24h window starts when VNPay confirmed,
        // giving the Owner a full 24h regardless of how long VNPay took to process the payment.
        //
        // Combining the Status/null-check predicates with the PaymentConfirmedAt comparison in
        // one Where doesn't translate under the SQLite provider used in tests — filter
        // server-side on the simple predicates, the date client-side (same limitation
        // documented throughout this codebase).
        var overdue = (await _ctx.Donations
                .Where(d => d.Status == DonationStatus.PendingOwnerAck && d.PaymentConfirmedAt != null)
                .ToListAsync(ct))
            .Where(d => d.PaymentConfirmedAt <= cutoff)
            .ToList();

        if (overdue.Count == 0) return;

        foreach (var donation in overdue)
        {
            donation.Status = DonationStatus.OwnerReceived;
            donation.AutoConfirmed = true;
            donation.OwnerAckAt = DateTimeOffset.UtcNow;
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
