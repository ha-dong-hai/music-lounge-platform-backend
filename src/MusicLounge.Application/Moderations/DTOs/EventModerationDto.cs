namespace MusicLounge.Application.Moderations.DTOs;

public sealed record EventModerationDto(
    int Id,
    string TargetType,
    int TargetId,
    float? AiScore,
    string? RiskLevel,
    string? FlagReason,
    string? AiRecommendation,
    int? AdminId,
    string? AdminDecision,
    string? ReviewNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SlaDeadline,
    DateTimeOffset? ReviewedAt
);
