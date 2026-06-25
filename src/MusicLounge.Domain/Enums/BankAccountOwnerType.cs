// CoreFlow: CF6 (Payment & Revenue)
// Identifies whose bank account this record belongs to.
// Used in the polymorphic bank_accounts table (owner_type + owner_id).
namespace MusicLounge.Domain.Enums;

public enum BankAccountOwnerType
{
    // Settlement payouts go to this account
    Lounge = 1,
    // Donation payouts go to this account
    Performer = 2
}
