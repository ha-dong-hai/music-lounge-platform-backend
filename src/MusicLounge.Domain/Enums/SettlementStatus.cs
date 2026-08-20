namespace MusicLounge.Domain.Enums;

public enum SettlementStatus
{
    Scheduled,      // waiting for release date
    Released,       // funds transferred to lounge
    Cancelled,      // cancelled (show cancelled / refunded)
    PendingReview   // D16: actual_duration < threshold — Admin must decide
}
