// CoreFlow: CF5 (Interaction & Feedback)
// Complaint filed by a user (or anonymous performer) against a venue, show, ticket, or penalty.
// Required by NĐ 85/2021 — platform must provide a formal complaint channel.
// ContactPhone supports guest reporters (performers without accounts) filing donation complaints (see D17).
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Complaint : BaseEntity<int>
{
    // Nullable — NULL for guest reporters (performers without accounts); SET NULL on account deletion (BVDLCN 2025)
    public int? ComplainantUserId { get; set; }
    public ComplaintTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public ComplaintCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    // JSON array of image URLs uploaded as evidence
    public string? EvidenceUrls { get; set; }
    // Phone number provided by guest reporter — Admin uses this to verify identity
    public string? ContactPhone { get; set; }
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;
    public int? AdminId { get; set; }
    public string? Resolution { get; set; }
    public ComplaintResolvedAction? ResolvedAction { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
