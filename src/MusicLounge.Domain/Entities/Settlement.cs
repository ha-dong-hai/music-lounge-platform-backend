// CoreFlow: CF6 (Payment & Revenue)
// Scheduled payout record for releasing collected ticket revenue to the venue Owner.
// Two-stage process: partial release before show, final release after show (see D3).
// Snapshot fields preserve the rates at creation time — config changes do not affect existing settlements (D12).
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Settlement : BaseEntity<int>
{
    public int PaymentId { get; set; }
    public SettlementReleaseType ReleaseType { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ScheduledDate { get; set; }
    // Snapshot of the pre-show release rate applied at creation time
    public decimal PreRateApplied { get; set; }
    // Snapshot of the post-show release rate applied at creation time
    public decimal PostRateApplied { get; set; }
    // Snapshot of the destination bank account — preserved for dispute resolution (D12)
    public int? BankAccountId { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    // Links to the ledger journal created when this settlement was released
    public string? LedgerJournalId { get; set; }
}
