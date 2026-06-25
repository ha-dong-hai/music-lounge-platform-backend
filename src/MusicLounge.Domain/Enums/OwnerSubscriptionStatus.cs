// CoreFlow: CF6 (Payment & Revenue)
// Current state of an Owner's subscription to a platform package.
namespace MusicLounge.Domain.Enums;

public enum OwnerSubscriptionStatus
{
    Active = 1,
    // Temporarily blocked due to a venue penalty — resumes after suspension_days
    Suspended = 2,
    // Billing period ended and was not renewed
    Expired = 3,
    // Owner manually cancelled before expiry
    Cancelled = 4
}
