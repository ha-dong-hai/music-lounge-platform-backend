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
    FnbOrderUpdate,         // Staff moved an F&B order to Preparing/Served/Cancelled
    // Refund left pending past refund_sla_hours, or nearing VNPay's hard refund window after which
    // the gateway refuses the reversal entirely. Stored as a string (max 50 chars) like every other
    // value here, so appending is safe without a migration.
    RefundSlaBreached,
    // Outcome of an identity or tax-profile review. The submitter has no other way to learn it:
    // they hand over a citizen card and a tax code and then, without this, hear nothing back.
    KycReviewResult,
    // Lenh tam khoa da phuc vu du han va duoc go. Khac PenaltyIssued (luc bi khoa) va
    // AppealResolved (khi khieu nai duoc xu) — day la duong ket thuc khong can ai lam gi ca.
    PenaltyExpired
}
