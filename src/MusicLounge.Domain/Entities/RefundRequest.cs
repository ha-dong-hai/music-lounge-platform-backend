// CoreFlow: CF3 (Ticket Booking), CF6 (Payment & Revenue)
// Buyer's request to refund a payment — reviewed and processed by Admin.
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class RefundRequest : BaseEntity<int>
{
    public int PaymentId { get; set; }
    // Nullable — SET NULL when requester account is deleted (BVDLCN 2025)
    public int? RequestedBy { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal AmountRequested { get; set; }
    // Set by Admin when approving — may differ from amount requested
    public decimal? AmountApproved { get; set; }
    // Percentage of ticket price to refund — derived from show refund policy
    public decimal? RefundPercentage { get; set; }
    public string Status { get; set; } = "pending";
    public int? ProcessedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
