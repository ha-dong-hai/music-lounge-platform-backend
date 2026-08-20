using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// D8: Logic ledger account — NOT a bank account
// Gateway/Platform/Tax accounts have OwnerId = null
public sealed class Account : Common.BaseEntity<int>
{
    public AccountType OwnerType { get; set; }
    public int? OwnerId { get; set; }   // null for Gateway/Platform/Tax; Users.Id or Performers.Id otherwise

    public ICollection<LedgerEntry> Entries { get; set; } = [];
}
