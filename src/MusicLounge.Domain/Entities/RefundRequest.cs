using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class RefundRequest : Common.BaseEntity<int>
{
    public int PaymentId { get; set; }
    public int? RequestedBy { get; set; }           // BVDLCN: SET NULL on user delete
    public string Reason { get; set; } = string.Empty;
    public decimal AmountRequested { get; set; }
    public decimal? AmountApproved { get; set; }
    public decimal? RefundPercentage { get; set; }
    public RefundRequestStatus Status { get; set; } = RefundRequestStatus.Pending;
    public int? ProcessedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public Payment Payment { get; set; } = null!;
    public User? Requester { get; set; }
    public User? Processor { get; set; }
}
