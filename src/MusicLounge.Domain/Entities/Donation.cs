using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Donation : Common.BaseEntity<int>
{
    public int? DonorUserId { get; set; }
    public int PerformanceId { get; set; }

    public decimal Gross { get; set; }
    public decimal Net { get; set; }

    public DonationStatus Status { get; set; } = DonationStatus.PendingPayment;
    public bool AutoConfirmed { get; set; } = false;
    public DateTimeOffset? OwnerAckAt { get; set; }
    public DateTimeOffset? OwnerPaidAt { get; set; }

    public string? PaymentRef { get; set; }
    public string? PaymentEvidenceUrl { get; set; }

    public bool IsAnonymous { get; set; } = false;
    public string? DisplayName { get; set; }
    public bool IsAmountPublic { get; set; } = true;
    public string? Message { get; set; }
    public bool IsMessagePublic { get; set; } = true;

    public string? GatewayRef { get; set; }
    public int? BankAccountId { get; set; }                 // D12: FK snapshot — performer bank account paid to (W31)

    // D12: snapshot of system_config.donation_performer_share_rate, set once ProcessDonationPaymentCommandHandler
    // confirms the donation is real (same moment Net becomes authoritative). ConfirmDonationPaidCommandHandler
    // (chặng 2 — the actual owner-to-performer forward, which can happen days/weeks later) reads THIS, not a
    // fresh system_config lookup, so a later Admin rate change can't retroactively change what a performer
    // was promised for a donation that already happened. Null only for donations created before this column
    // existed — those fall back to a live config read as before.
    public decimal? PerformerShareRateSnapshot { get; set; }

    // Set when VNPay confirms payment. The 24h auto-confirm window starts from here, not CreatedAt.
    public DateTimeOffset? PaymentConfirmedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? Donor { get; set; }
    public Performance Performance { get; set; } = null!;
    public BankAccount? BankAccount { get; set; }
}
