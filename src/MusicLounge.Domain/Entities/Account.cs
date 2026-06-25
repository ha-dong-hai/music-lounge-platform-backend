// CoreFlow: CF6 (Payment & Revenue)
// Logical ledger account — NOT a bank account. Used in double-entry bookkeeping.
// Types: Gateway (VNPay), Platform (our revenue), Tax (withheld tax), User, Performer.
// Each journal entry debits one account and credits another; SUM(debit) must equal SUM(credit).
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Account : BaseEntity<int>
{
    public AccountType OwnerType { get; set; }
    // Null for system accounts (Gateway, Platform, Tax); set for User and Performer accounts
    public int? OwnerId { get; set; }
}
