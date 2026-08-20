using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class BankAccount : Common.BaseEntity<int>
{
    public BankAccountOwnerType OwnerType { get; set; }
    public int OwnerId { get; set; }    // polymorphic: lounge.id or performer.id — no FK
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = true;
    public bool IsVerified { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
}
