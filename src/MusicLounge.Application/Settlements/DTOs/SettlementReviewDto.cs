namespace MusicLounge.Application.Settlements.DTOs;

/// <summary>
/// Một khoản quyết toán đang chờ Admin quyết, kèm <b>đúng bằng chứng đã khiến nó bị giữ lại</b>.
///
/// <para>Admin phải trả lời được một câu: buổi diễn này có thật sự chạy đủ như đã hứa với người mua
/// vé không. Nên DTO mang cả thời lượng dự kiến, thời lượng thật, tỉ lệ, và ngưỡng đang áp — thay
/// vì bắt Admin đi tra thủ công rồi tự tính lại một con số có thể lệch.</para>
/// </summary>
/// <param name="Ratio">
/// <c>null</c> nghĩa là không kết luận được từ dữ liệu (buổi diễn chưa từng được đánh dấu bắt đầu
/// hoặc kết thúc), khác hẳn với tỉ lệ thấp.
/// </param>
public sealed record SettlementReviewDto(
    int SettlementId,
    int OwnerId,
    int PaymentId,
    string ReleaseType,
    decimal GrossAmount,
    decimal NetAmount,
    DateTimeOffset ScheduledAt,
    int? ShowId,
    string? ShowName,
    DateTimeOffset? ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    DateTimeOffset? ActualStart,
    DateTimeOffset? ActualEnd,
    decimal? Ratio,
    decimal Threshold,
    bool HasPendingRefund);
