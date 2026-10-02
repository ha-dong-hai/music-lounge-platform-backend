using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.VenuePenalties.DTOs;

public sealed record VenuePenaltyDto(
    Guid Id,
    Guid LoungeId,
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
