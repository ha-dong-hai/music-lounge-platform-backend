using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class EventModeration : Common.BaseEntity<int>
{
    public ModerationTargetType TargetType { get; set; }
    public int TargetId { get; set; }

    public float? AiScore { get; set; }
    public ModerationRiskLevel? RiskLevel { get; set; }
    public string? FlagReason { get; set; }
    public AiModerationRecommendation? AiRecommendation { get; set; }

    public int? AdminId { get; set; }
    public ModerationDecision? AdminDecision { get; set; }
    public string? ReviewNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public User? Admin { get; set; }
}
