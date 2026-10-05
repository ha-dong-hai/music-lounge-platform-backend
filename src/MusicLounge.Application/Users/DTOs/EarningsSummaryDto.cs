namespace MusicLounge.Application.Users.DTOs;

public sealed record EarningsSummaryDto(
    decimal TotalEarned,
    decimal PendingSettlement,
    decimal CompletedSettlement,
    int PendingSettlementCount,
    IReadOnlyList<RecentSettlementDto> RecentSettlements);

public sealed record RecentSettlementDto(
    Guid Id,
    decimal Amount,
    string Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? PaidAt)
{
    // MLACP-658: khoản này là tiền gì ("Tiền vé đợt 70% — Đêm tình ca Trịnh · 2 vé"). Trước đây danh sách chỉ có số tiền
    // và ngày — chủ phòng trà không biết khoản nào của buổi nào. Không đặt vào tham số vị trí để không đổi các chỗ dựng DTO.
    public string? Title { get; init; }
}
