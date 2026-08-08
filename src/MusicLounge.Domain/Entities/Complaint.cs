using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// W30, D17, NĐ 85/2021
public sealed class Complaint : Common.BaseEntity<int>
{
    public int? ComplainantUserId { get; set; }     // BVDLCN: SET NULL on delete; null = guest reporter
    public string TargetType { get; set; } = string.Empty;  // "show", "venue", "donation", "ticket", "penalty"
    public int TargetId { get; set; }               // polymorphic — no FK
    public ComplaintCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }       // JSON array of URLs
    public string? ContactPhone { get; set; }       // D17: guest reporter without account
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;
    public int? AdminId { get; set; }
    public string? Resolution { get; set; }
    public ComplaintResolvedAction? ResolvedAction { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? Complainant { get; set; }
    public User? Admin { get; set; }
}
