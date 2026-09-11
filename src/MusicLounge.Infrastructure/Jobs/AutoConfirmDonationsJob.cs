using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Donations;
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
        var candidates = await _ctx.Donations
            .Where(d => d.Status == DonationStatus.PendingOwnerAck && d.PaymentConfirmedAt != null)
            .ToListAsync(ct);
        if (candidates.Count == 0) return;

        // MLACP-361: donate co khoan quyet toan thi "da nhan" chi co nghia sau khi nen tang da chuyen
        // tien — nen dong ho tu xac nhan tinh tu luc giai ngan, va chua giai ngan thi khong tu xac nhan.
        // Tinh tu luc VNPay xac nhan nhu truoc se danh dau "phong tra da nhan" mot khoan con nam o
        // nen tang (vi du phong tra chua co tai khoan ngan hang). Donate cu khong co khoan quyet toan:
        // giu dong ho cu.
        var referenceIds = candidates.Select(d => d.Id.ToString()).ToList();
        var payouts = await _ctx.Payments
            .Where(p => p.ReferenceType == DonationPayouts.PaymentReferenceType && referenceIds.Contains(p.ReferenceId))
            .Join(_ctx.Settlements, p => p.Id, s => s.PaymentId,
                (p, s) => new { p.ReferenceId, s.Status, s.ReleasedAt })
            .ToListAsync(ct);
        var releasedAtByDonation = payouts.ToDictionary(
            x => int.Parse(x.ReferenceId),
            x => x.Status == SettlementStatus.Released ? x.ReleasedAt : null);

        var overdue = candidates
            .Where(d => releasedAtByDonation.TryGetValue(d.Id, out var releasedAt)
                ? releasedAt is not null && releasedAt <= cutoff
                : d.PaymentConfirmedAt <= cutoff)
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
