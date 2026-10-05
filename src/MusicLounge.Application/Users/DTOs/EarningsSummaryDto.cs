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

    // MLACP-662: khoản chưa chuyển mà khoản thanh toán gốc còn yêu cầu hoàn tiền đang chờ — SettlementReleaseJob sẽ KHÔNG
    // chuyển nó tới khi yêu cầu được xử lý (hoàn xong thì khoản bị thu nhỏ hoặc về 0). Trước đây màn hình vẫn ghi "Đã lên
    // lịch · dự kiến chuyển <ngày>" cho khoản như vậy (đo 05/10/2026: 4 khoản của buổi tự hoàn vì chỉ phát 2% thời lượng).
    public bool HeldForRefund { get; init; }
}
