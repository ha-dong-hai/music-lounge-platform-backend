// CoreFlow: CF6 (Payment & Revenue)
// Stores bank account details for venues and performers to receive payouts.
// Polymorphic — owner_type + owner_id together identify whose account this is.
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class BankAccount : BaseEntity<int>
{
    public BankAccountOwnerType OwnerType { get; set; }
    // Points to music_lounges.id or performers.id depending on OwnerType — no FK enforced (polymorphic)
    public int OwnerId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    // Only one default account per owner — enforced at application layer
    public bool IsDefault { get; set; } = true;
    // Admin must verify before payouts can be sent to this account
    public bool IsVerified { get; set; } = false;
    public DateTime CreatedAt { get; set; }
}
