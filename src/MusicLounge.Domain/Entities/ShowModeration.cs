// CoreFlow: CF1 (Event Management), CF4 (Livestream Participation)
// Moderation record created when a show or livestream is submitted for Admin review.
// AI provides a recommendation and risk score, but Admin always makes the final decision (see D11).
// SLA: Admin must decide within 24 hours (NĐ 147/2024).
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class ShowModeration : BaseEntity<int>
{
    public ShowModerationTargetType TargetType { get; set; }
    // Points to events.id or livestreams.id depending on TargetType — no FK enforced (polymorphic)
    public int TargetId { get; set; }
    // AI content safety score: 0 = safe, 1 = dangerous
    public decimal? AiScore { get; set; }
    public RiskLevel? RiskLevel { get; set; }
    public string? FlagReason { get; set; }
    // Advisory only — Admin may override (see D11)
    public AiRecommendationLevel? AiRecommendation { get; set; }
    // Null until Admin acts
    public int? AdminId { get; set; }
    // This is the only field with legal authority — AI recommendation is not binding
    public AdminDecision? AdminDecision { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
