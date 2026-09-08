namespace MusicLounge.Application.Moderations.DTOs;

public sealed record ContentReportQueueItemDto(
    string TargetType,
    int TargetId,
    string? TargetSummary,
    int ReportCount,
    string LatestReason,
    DateTimeOffset EarliestReportedAt,
    DateTimeOffset SlaDeadline
);
