using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Settlement : Common.BaseEntity<int>
{
    public int OwnerId { get; set; }
    public int PaymentId { get; set; }
    public SettlementReleaseType ReleaseType { get; set; }  // D3: partial_70 | final_30
    public decimal GrossAmount { get; set; }                // D12: snapshot — original payment gross
    public decimal PreRateApplied { get; set; }             // D12: snapshot — e.g. 0.70 (partial tranche %)
    public decimal PostRateApplied { get; set; }            // D12: snapshot — e.g. 0.30 (final tranche %)
    public decimal NetAmount { get; set; }                  // actual amount released this tranche
    public int? BankAccountId { get; set; }                 // D12: snapshot FK — lounge bank account at settlement time
    public SettlementStatus Status { get; set; } = SettlementStatus.Scheduled;
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? LedgerJournalId { get; set; }            // links to ledger_entry.journal_id when released
    public string? PaymentReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Payment Payment { get; set; } = null!;
    public BankAccount? BankAccount { get; set; }
}
