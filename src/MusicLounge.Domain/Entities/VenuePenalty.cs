// CoreFlow: CF5 (Interaction & Feedback)
// Penalty issued by Admin against a venue for policy violations.
// Owner can appeal within the appeal_deadline — Admin reviews and records the result.
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class VenuePenalty : BaseEntity<int>
{
    public int LoungeId { get; set; }
    public PenaltyType PenaltyType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceRef { get; set; }
    public int IssuedBy { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime EffectiveAt { get; set; }
    // Set for Suspension penalties only — how many days the venue is blocked
    public int? SuspensionDays { get; set; }
    public DateTime? SuspensionEnd { get; set; }
    public PenaltyStatus Status { get; set; } = PenaltyStatus.Active;
    public DateTime? AppealDeadline { get; set; }
    public DateTime? AppealedAt { get; set; }
    // No pending state — use PenaltyStatus.Appealed while under review
    public AppealResult? AppealResult { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? CompensationNote { get; set; }
}
