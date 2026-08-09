using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Payment : Common.BaseEntity<int>
{
    public string OrderId { get; set; } = string.Empty;     // vnp_TxnRef — unique
    public int? PayerId { get; set; }                        // FK→users SET NULL (BVDLCN 2025)
    public decimal GrossAmount { get; set; }                 // buyer-facing amount
    public decimal GatewayFee { get; set; }                  // VNPay processing fee
    public decimal PlatformFee { get; set; }                 // platform commission
    public decimal TaxWithheld { get; set; }                 // VAT/FCIT withheld
    public decimal NetAmount { get; set; }                   // gross - gatewayFee - platformFee - tax
    public PaymentMethod Method { get; set; } = PaymentMethod.Gateway;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public PaymentSettlementStatus SettlementStatus { get; set; } = PaymentSettlementStatus.NotApplicable;
    public string? IdempotencyKey { get; set; }              // prevent double-charge across all payment types
    public string? TransactionId { get; set; }              // vnp_TransactionNo
    public string? VnPayResponseCode { get; set; }
    public string ReferenceType { get; set; } = string.Empty;  // "TicketHold"
    public string ReferenceId { get; set; } = string.Empty;    // holdId.ToString()

    // D12-style snapshot for Subscription payments only (null for every other ReferenceType) —
    // captured at checkout (SubscribeToPackageCommandHandler) so an admin editing
    // SubscriptionPackage.MaxTicketsPerEvent/HasAiPoster between checkout and VNPay confirming
    // can't change what the owner actually receives for what they already paid for. Mirrors why
    // GrossAmount itself is captured once at checkout instead of re-read from the package later.
    public int? SubscriptionMaxTicketsPerEventSnapshot { get; set; }
    public bool? SubscriptionHasAiPosterSnapshot { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public User? Payer { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = [];
}
