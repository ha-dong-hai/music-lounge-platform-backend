namespace MusicLounge.Domain.Enums;

public enum ComplaintCategory
{
    EventMisrepresentation,
    RefundDispute,
    DonationNotPaid,    // D17: performer not paid
    TechnicalIssue,
    VenueConduct,
    PenaltyAppeal,
    Other,
    // MLACP-192: noi dung vi pham noi quy phat trong livestream (bao luc, khieu dam, ngon tu thu
    // ghet...) - rieng biet voi VenueConduct (thai do nhan vien/co so vat chat that) va
    // EventMisrepresentation (quang cao sai su that).
    ContentViolation
}
