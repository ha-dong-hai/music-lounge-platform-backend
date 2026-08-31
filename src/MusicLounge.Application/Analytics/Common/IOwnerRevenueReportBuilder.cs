using MusicLounge.Application.Analytics.DTOs;

namespace MusicLounge.Application.Analytics.Common;

// Single source of truth for the revenue-report aggregation (ticket + F&B + donate, by event and
// by month) — GetOwnerRevenueReportQueryHandler (screen view) and ExportOwnerRevenueReportQueryHandler
// (file export) both call this instead of each computing the totals independently, so the numbers
// a Owner sees on screen and the numbers in the file they download can never drift apart. Same
// "single write/compute point" reasoning as PaymentFeeCalculator/ILedgerService elsewhere in this
// codebase. Callers are responsible for their own venue-ownership authorization check.
public interface IOwnerRevenueReportBuilder
{
    Task<OwnerRevenueReportDto> BuildAsync(
        int loungeId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}
