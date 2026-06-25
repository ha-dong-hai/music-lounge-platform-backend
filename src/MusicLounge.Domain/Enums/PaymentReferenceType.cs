// CoreFlow: CF3 (Ticket Booking), CF6 (Payment & Revenue)
// Identifies what the payment record is associated with.
// Used in the polymorphic payments table (reference_type + reference_id).
namespace MusicLounge.Domain.Enums;

public enum PaymentReferenceType
{
    Ticket = 1,
    Donation = 2,
    Fnb = 3,
    Subscription = 4,
    Refund = 5
}
