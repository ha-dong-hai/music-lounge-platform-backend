namespace MusicLounge.Domain.Enums;

public enum NotificationType
{
    TicketConfirmed,
    EventReminder,
    EventRescheduled,
    EventCancelled,
    EventFormatChanged,
    EventLive,
    NewEvent,
    WishlistLowStock,
    DonationReceived,
    DonationPending,        // > 7 days unpaid to performer
    SettlementReleased,
    ModerationResult,
    PenaltyWarning,
    PenaltyIssued,
    AppealResolved,
    ComplaintUpdate,
    SubscriptionExpiring,   // D14: 30/7/1 day before expiry
    DuplicatePaymentDetected, // owner double-submitted Subscribe and both VNPay payments succeeded
    ModerationSlaBreached,  // NĐ 147/2024: flagged content past its review deadline, still undecided
    SecurityAlert           // credential-stuffing spike, unexpected new Admin, other security drift
}
