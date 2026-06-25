// CoreFlow: CF3 (Ticket Booking), CF4 (Livestream), CF5 (Interaction), CF6 (Payment)
// Type of push notification sent to a user via FCM.
// Used to build deep link navigation on the mobile app.
namespace MusicLounge.Domain.Enums;

public enum NotificationType
{
    // CF3 — ticket purchase confirmed, QR code ready
    TicketConfirmed = 1,
    // CF3 — reminder before show starts
    EventReminder = 2,
    // CF4 — show has gone live, viewer can join now
    EventLive = 3,
    // CF2 — a venue the user follows published a new show
    NewEvent = 4,
    // CF3 — a wishlisted show is running low on tickets
    WishlistLowStock = 5,
    // CF6 — Owner received a donation for their performer
    DonationReceived = 6,
    // CF6 — Owner has not paid performer after 7 days (see D4)
    DonationPending = 7,
    // CF6 — settlement amount released to Owner's bank account
    SettlementReleased = 8,
    // CF1 — Admin approved or rejected a show submission
    ModerationResult = 9,
    // CF5 — venue received a penalty warning from Admin
    PenaltyWarning = 10,
    // CF5 — update on a complaint the user filed
    ComplaintUpdate = 11,
    // CF6 — Owner subscription is expiring soon (30/7/1 day warnings)
    SubscriptionExpiring = 12
}
