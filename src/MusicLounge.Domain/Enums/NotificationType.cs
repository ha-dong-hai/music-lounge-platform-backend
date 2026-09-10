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
    PenaltyExpired,
    // Ket qua duyet ho so phong tra. Owner khong co duong nao khac de biet: ho nop giay phep kinh
    // doanh len roi cho, va truoc MLACP-307 thi cho mai mai vi khong ai duyet duoc.
    VenueReviewResult,

    /// <summary>
    /// MLACP-334: VNPay bao thanh cong cho mot ban ghi da bi dong (Failed/Cancelled) — tien that da
    /// thu ma he thong khong cap duoc gi. Can nguoi doi soat roi cap lai hoac hoan tien.
    /// </summary>
    PaymentConfirmedAfterExpiry,

    /// <summary>
    /// MLACP-335: chot D16 vua giu lai mot khoan quyet toan. Truoc day job chi doi trang thai roi
    /// di tiep — khong ai biet co khoan dang cho quyet, va khong co duong nao de quyet.
    /// </summary>
    SettlementPendingReview,

    /// <summary>MLACP-335: Admin da quyet khong chi tra tranche nay.</summary>
    SettlementWithheld,

    /// <summary>
    /// MLACP-337: yeu cau hoan tien cua nguoi mua da duoc quyet (duyet hoac tu choi). Truoc do
    /// ProcessRefundRequest khong bao cho ai ca — nguoi mua gui yeu cau roi phai tu di hoi.
    /// Mot loai cho ca hai ket qua, theo dung tien le ComplaintUpdate.
    /// </summary>
    RefundUpdate,

    /// <summary>
    /// MLACP-337: ve ban tai quay thu tien mat, nen tang chua bao gio giu khoan do. Hoan tien cho
    /// nhung ve nay la nghia vu cua chinh phong tra, tra truc tiep cho khach.
    /// </summary>
    RefundOwedByVenue,

    /// <summary>
    /// MLACP-339: da toi gio dien ma buoi dien van chua duoc bam Bat dau. Khong bam thi nhan vien
    /// khong quet duoc ve o cua, khan gia khong donate duoc, va sau do khong ai danh gia duoc —
    /// ba thu deu doi show.Status == Ongoing.
    /// </summary>
    ShowNotStarted,

    /// <summary>
    /// MLACP-341: buoi dien da qua gio ma chua tung duoc bat dau, va no co ve offline nen he thong
    /// KHONG ket luan duoc la co dien ra hay khong. Gui cho chu phong tra truoc, roi cho nguoi mua
    /// sau mot cua so — de mot lan quen bam nut khong bien thanh mot tin xau gui cho ca khan phong.
    /// </summary>
    ShowDeliveryUnconfirmed,
    // MLACP-347: livestream da len song roi bi cat ngang (mat ket noi qua han, bi go, hoac ket
    // thuc som) - bao nguoi mua ve livestream va chu phong tra.
    LivestreamCutShort
}
