namespace MusicLounge.Application.Donations.DTOs;

/// <summary>MLACP-363 — một dòng trong nhật ký bằng chứng của một khoản donate.</summary>
public sealed record DonationEventDto(
    int Sequence,
    string EventType,
    DateTimeOffset OccurredAt,
    int? ActorUserId,
    decimal? Amount,
    string? Reference,
    string? EvidenceUrl,
    string? EvidenceSha256,
    string? Detail,
    string? PreviousHash,
    string Hash);

/// <param name="ChainIntact">False nếu có dòng bị sửa, bị xoá hoặc bị chèn sau khi ghi.</param>
/// <param name="FirstBrokenSequence">Dòng đầu tiên không khớp chuỗi băm.</param>
public sealed record DonationEvidenceDto(
    int DonationId,
    bool ChainIntact,
    int? FirstBrokenSequence,
    IReadOnlyList<DonationEventDto> Events);
