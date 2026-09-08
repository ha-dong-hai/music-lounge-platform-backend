using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// D1: AuditableEntity (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy, auto-stamped by
// ApplicationDbContext.SaveChangesAsync) — this is the payout-destination record for real money
// (settlement/donation), so knowing who added/changed an account number matters more here than on
// most reference-data entities.
public sealed class BankAccount : Common.AuditableEntity<int>
{
    public BankAccountOwnerType OwnerType { get; set; }
    public int OwnerId { get; set; }    // polymorphic: lounge.id or performer.id — no FK
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = true;
    public bool IsVerified { get; set; } = false;
}
