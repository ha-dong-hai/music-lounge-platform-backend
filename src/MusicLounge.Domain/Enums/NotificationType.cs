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
    SubscriptionExpiring    // D14: 30/7/1 day before expiry
}
