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
    ContentReportSlaBreached, // NĐ 147/2024: user-reported content past its 48h takedown deadline
    SecurityAlert,          // credential-stuffing spike, unexpected new Admin, other security drift
    FnbOrderUpdate          // Staff moved an F&B order to Preparing/Served/Cancelled
}
