// CoreFlow: CF6 (Payment & Revenue)
// IMMUTABLE double-entry bookkeeping record — append only, never UPDATE or DELETE.
// Each journal must satisfy: SUM(debit) = SUM(credit) across all entries sharing the same JournalId.
// To correct an error, write a reversal entry — never modify existing rows (see D8).
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class LedgerEntry : BaseEntity<int>
{
    // Groups related debit/credit lines into one balanced journal transaction
    public string JournalId { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public decimal Debit { get; set; } = 0;
    public decimal Credit { get; set; } = 0;
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public string? Description { get; set; }
    // No UpdatedAt — this record must never be changed after creation
    public DateTime CreatedAt { get; set; }
}
