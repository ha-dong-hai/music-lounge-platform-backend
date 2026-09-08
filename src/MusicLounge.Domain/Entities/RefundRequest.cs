using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// AuditableEntity (CreatedAt/CreatedBy/UpdatedAt/UpdatedBy, auto-stamped) — CreatedBy is who/what
// TRIGGERED this row (e.g. an Owner cancelling a show, an Admin resolving a complaint), distinct
// from RequestedBy below (the buyer the refund is FOR) — most creation paths set these to two
// different users, so CreatedBy is not redundant with the existing domain fields.
public sealed class RefundRequest : Common.AuditableEntity<int>
{
    public int PaymentId { get; set; }
    public int? RequestedBy { get; set; }           // BVDLCN: SET NULL on user delete
    public string Reason { get; set; } = string.Empty;
    public decimal AmountRequested { get; set; }
    public decimal? AmountApproved { get; set; }
    public decimal? RefundPercentage { get; set; }
    public RefundRequestStatus Status { get; set; } = RefundRequestStatus.Pending;
    public int? ProcessedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public Payment Payment { get; set; } = null!;
    public User? Requester { get; set; }
    public User? Processor { get; set; }
}
