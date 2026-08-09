using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// D4: Owner không xác nhận trong donation_hold_days ngày → auto-confirm + flag auto_confirmed=true
/// cho Admin biết. Window is config-driven (default 7 days) rather than hardcoded — research against
/// comparable escrow-style intermediary-confirmation patterns (Upwork: 14-day auto-release if
/// unresponded; Fiverr: 3-day response window + 14-day grace period) found the originally-hardcoded
/// 24h here to be far more aggressive than any real comparable, while the 7-day value already seeded
/// in system_config under this exact key was never actually wired in until now.
/// </summary>
public sealed class AutoConfirmDonationsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;

    public AutoConfirmDonationsJob(ApplicationDbContext ctx, ISystemConfigService config)
    {
        _ctx = ctx;
        _config = config;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var holdDays = await _config.GetIntAsync(ConfigKeys.DonationHoldDays, 7, ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-holdDays);

        // Use PaymentConfirmedAt (not CreatedAt) so the hold window starts when VNPay confirmed,
        // giving the Owner the full window regardless of how long VNPay took to process the payment.
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
