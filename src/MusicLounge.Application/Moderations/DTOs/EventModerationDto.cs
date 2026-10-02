namespace MusicLounge.Application.Moderations.DTOs;

public sealed record EventModerationDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    float? AiScore,
    string? RiskLevel,
    string? FlagReason,
    string? AiRecommendation,
    Guid? AdminId,
    string? AdminDecision,
    string? ReviewNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SlaDeadline,
    DateTimeOffset? ReviewedAt
);
