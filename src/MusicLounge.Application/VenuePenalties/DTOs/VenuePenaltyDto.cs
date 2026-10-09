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
    DateTimeOffset? ReviewedAt,
    // MLACP-702: hạn cuối để chủ phòng trà GỬI khiếu nại (IssuedAt + penalty_appeal_window_days). Khác
    // AppealDeadline là hạn Admin xử lý, chỉ có sau khi khiếu nại. Null khi không còn khiếu nại được
    // (đã khiếu nại, hoặc án không còn Active) và ở danh sách của Admin (không dùng).
    DateTimeOffset? AppealWindowEndsAt = null);
