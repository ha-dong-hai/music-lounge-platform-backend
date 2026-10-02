namespace MusicLounge.Domain.Entities;

// D8: Append-only — no UpdatedAt, never UPDATE or DELETE. SUM(debit) == SUM(credit) per JournalId.
// Every entry belongs to a logical ledger account (D8) and traces back to its business origin
// via ReferenceType/ReferenceId; PaymentId is set only when the journal originates from a gateway payment.
public sealed class LedgerEntry : Common.BaseEntity<Guid>
{
    public string JournalId { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public bool IsDebit { get; set; }
    public string ReferenceType { get; set; } = string.Empty;   // "payment" | "settlement" | "donation" | "refund"
    public string ReferenceId { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Account Account { get; set; } = null!;
    public Payment? Payment { get; set; }
}
