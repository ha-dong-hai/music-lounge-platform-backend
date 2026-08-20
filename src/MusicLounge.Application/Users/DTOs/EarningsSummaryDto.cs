namespace MusicLounge.Application.Users.DTOs;

public sealed record EarningsSummaryDto(
    decimal TotalEarned,
    decimal PendingSettlement,
    decimal CompletedSettlement,
    int PendingSettlementCount,
    IReadOnlyList<RecentSettlementDto> RecentSettlements);

public sealed record RecentSettlementDto(
    int Id,
    decimal Amount,
    string Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? PaidAt);
