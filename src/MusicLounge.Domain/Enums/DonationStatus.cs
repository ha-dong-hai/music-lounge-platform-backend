// CoreFlow: CF6 (Payment & Revenue)
// Lifecycle of a donation from audience to performer, flowing through Owner.
// See D4 in complete_reference.md for the 2-stage donation flow.
namespace MusicLounge.Domain.Enums;

public enum DonationStatus
{
    // VNPay confirmed payment — waiting for Owner to acknowledge receipt
    PendingOwnerAck = 1,
    // Owner confirmed they received the donation — 7-day window to pay performer starts
    OwnerReceived = 2,
    // Owner confirmed payment to performer — donation flow complete
    PerformerPaid = 3
}
