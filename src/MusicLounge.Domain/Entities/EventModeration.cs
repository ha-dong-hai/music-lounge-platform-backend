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
    // NĐ 147/2024: SLA 24h to review flagged content. Set at creation from system_config's
    // moderation_sla_hours (never hardcoded — §6.7) — was previously never populated anywhere.
    // Nullable so the migration adding this column doesn't fabricate a deadline for rows that
    // existed before this field did — null there means "no SLA was ever computed", not "0 hours".
    public DateTimeOffset? SlaDeadline { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public User? Admin { get; set; }
}
