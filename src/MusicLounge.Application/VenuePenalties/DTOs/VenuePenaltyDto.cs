using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.VenuePenalties.DTOs;

public sealed record VenuePenaltyDto(
    int Id,
    int LoungeId,
    string LoungeName,
    PenaltyType PenaltyType,
    string Reason,
    string? EvidenceRef,
    DateTimeOffset IssuedAt,
    DateTimeOffset EffectiveAt,
    int? SuspensionDays,
    DateTimeOffset? SuspensionEnd,
    PenaltyStatus Status,
    DateTimeOffset? AppealDeadline,
    DateTimeOffset? AppealedAt,
    string? AppealReason,
    string? AppealResult,
    DateTimeOffset? ReviewedAt);
