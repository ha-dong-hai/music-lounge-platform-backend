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
///
/// <para>MLACP-361/362: hạn tính từ lúc phòng trà thật sự nhận tiền (<see cref="DonationPayoutDeadline"/>)
/// — cùng một mốc với hạn chuyển cho nghệ sĩ. Donate có khoản quyết toán chưa được giải ngân thì không
/// bao giờ tự xác nhận: không được đánh dấu "phòng trà đã nhận" một khoản còn nằm ở nền tảng.</para>
/// </summary>
public sealed class AutoConfirmDonationsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public AutoConfirmDonationsJob(ApplicationDbContext ctx, IUnitOfWork uow, ISystemConfigService config)
    {
        _ctx = ctx;
        _uow = uow;
        _config = config;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var holdDays = await DonationPayoutDeadline.HoldDaysAsync(_config, ct);
        var now = DateTimeOffset.UtcNow;

        // Combining the Status/null-check predicates with a DateTimeOffset comparison in one Where
        // doesn't translate under the SQLite provider used in tests — filter server-side on the
        // simple predicates, the date client-side (same limitation documented throughout this codebase).
        var candidates = await _ctx.Donations
            .Where(d => d.Status == DonationStatus.PendingOwnerAck && d.PaymentConfirmedAt != null)
            .ToListAsync(ct);
        if (candidates.Count == 0) return;

        var releaseTimes = await DonationPayoutDeadline.PayoutReleaseTimesAsync(
            _uow, candidates.Select(d => d.Id).ToList(), ct);

        var overdue = candidates
            .Where(d => DonationPayoutDeadline.DueAt(DonationPayoutDeadline.ReceivedAt(d, releaseTimes), holdDays) <= now)
            .ToList();
        if (overdue.Count == 0) return;

        foreach (var donation in overdue)
        {
            donation.Status = DonationStatus.OwnerReceived;
            donation.AutoConfirmed = true;
            // Chỉ là lúc hệ thống ghi nhận — không còn là mốc tính hạn trả nghệ sĩ (MLACP-362).
            donation.OwnerAckAt = now;
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
