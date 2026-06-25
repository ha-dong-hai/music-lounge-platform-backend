// CoreFlow: CF6 (Payment & Revenue)
// Records a donation from an audience member to a performer via the Owner.
// Follows a 2-stage flow: Audience → Owner → Performer (see D4 in complete_reference.md).
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Donation : BaseEntity<int>
{
    // Nullable — SET NULL when donor account is deleted (BVDLCN 2025)
    public int? DonorUserId { get; set; }
    // Links to a specific performer in a specific show
    public int PerformanceId { get; set; }
    public decimal Gross { get; set; }
    // Amount performer actually receives — used for Ledger journal J2
    public decimal Net { get; set; }
    public DonationStatus Status { get; set; } = DonationStatus.PendingOwnerAck;
    // True if Owner did not confirm within 24h — system auto-confirmed (flagged for Admin)
    public bool AutoConfirmed { get; set; } = false;
    public DateTime? OwnerAckAt { get; set; }
    public DateTime? OwnerPaidAt { get; set; }
    // Bank transfer reference when Owner pays performer
    public string? PaymentRef { get; set; }
    // Bank statement image uploaded by Owner as proof of payment
    public string? PaymentEvidenceUrl { get; set; }
    // Snapshot of performer bank account at time of payment — preserved for dispute resolution (D12)
    public int? BankAccountId { get; set; }
    public DateTime? PerformerConfirmedAt { get; set; }
    public bool IsAnonymous { get; set; } = false;
    public string? DisplayName { get; set; }
    public bool IsAmountPublic { get; set; } = true;
    public string? Message { get; set; }
    public bool IsMessagePublic { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
