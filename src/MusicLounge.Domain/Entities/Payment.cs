// CoreFlow: CF3 (Ticket Booking), CF6 (Payment & Revenue)
// Records every financial transaction on the platform.
// Polymorphic — reference_type + reference_id identify what was paid for.
// All monetary fields are snapshots taken at payment time and must never be updated (see D12).
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Payment : BaseEntity<int>
{
    public PaymentReferenceType ReferenceType { get; set; }
    // UUID string for ticket payments; integer string for all other types
    public string ReferenceId { get; set; } = string.Empty;
    // Nullable — SET NULL when payer account is deleted (BVDLCN 2025)
    public int? PayerId { get; set; }
    public decimal Gross { get; set; }
    public decimal GatewayFee { get; set; } = 0;
    public decimal PlatformFee { get; set; } = 0;
    public decimal TaxWithheld { get; set; } = 0;
    // Net = Gross - GatewayFee - PlatformFee - TaxWithheld
    public decimal Net { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public PaymentSettlementStatus SettlementStatus { get; set; } = PaymentSettlementStatus.NotApplicable;
    // VNPay or other gateway transaction reference
    public string? GatewayRef { get; set; }
    // Prevents duplicate payments on retry — checked before processing (NĐ 52/2024)
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
}
