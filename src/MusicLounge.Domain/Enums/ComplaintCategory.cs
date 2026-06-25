// CoreFlow: CF5 (Interaction & Feedback), CF6 (Payment & Revenue)
// Categorizes what type of issue the complaint describes.
// Required by NĐ 85/2021 — platform must provide a complaint channel.
namespace MusicLounge.Domain.Enums;

public enum ComplaintCategory
{
    // Show content did not match what was advertised
    EventMisrepresentation = 1,
    // Buyer did not receive refund they are entitled to
    RefundDispute = 2,
    // Performer reports not receiving donation payment from Owner (see D17)
    DonationNotPaid = 3,
    TechnicalIssue = 4,
    VenueConduct = 5,
    // Owner appeals a penalty issued by Admin
    PenaltyAppeal = 6,
    Other = 7
}
