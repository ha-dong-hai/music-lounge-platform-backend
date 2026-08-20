using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Refunds.DTOs;

public sealed record RefundRequestDto(
    int Id,
    int PaymentId,
    int? RequestedBy,
    string Reason,
    decimal AmountRequested,
    decimal? AmountApproved,
    decimal? RefundPercentage,
    RefundRequestStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
