using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Complaints.DTOs;

public sealed record ComplaintDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    ComplaintCategory Category,
    string Description,
    string? EvidenceUrls,
    string? ContactPhone,
    ComplaintStatus Status,
    string? ComplainantName,
    string? AdminName,
    string? Resolution,
    ComplaintResolvedAction? ResolvedAction,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt);
