// CoreFlow: CF6 (Payment & Revenue)
// Tracks how much of the payment has been released to the venue Owner.
// Progresses from collected → partially_released → fully_released after show completes.
namespace MusicLounge.Domain.Enums;

public enum PaymentSettlementStatus
{
    // Payment type does not involve settlement (e.g. F&B cash)
    NotApplicable = 1,
    // Money held in escrow — show has not happened yet
    Collected = 2,
    // 70% released before show (partial settlement)
    PartiallyReleased = 3,
    // Remaining 30% released after show completes
    FullyReleased = 4,
    // Payment was refunded to buyer — settlement cancelled
    Refunded = 5
}
