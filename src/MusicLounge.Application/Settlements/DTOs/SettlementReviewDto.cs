namespace MusicLounge.Application.Settlements.DTOs;

/// <summary>
/// Một khoản quyết toán đang chờ Admin quyết, kèm <b>đúng bằng chứng đã khiến nó bị giữ lại</b>.
///
/// <para>Admin phải trả lời được một câu: buổi diễn này có thật sự chạy đủ như đã hứa với người mua
/// vé không. Nên DTO mang cả thời lượng dự kiến, thời lượng thật, tỉ lệ, và ngưỡng đang áp — thay
/// vì bắt Admin đi tra thủ công rồi tự tính lại một con số có thể lệch.</para>
/// </summary>
/// <param name="Verdict">
/// Vì sao khoản này bị giữ lại. <c>NeverStarted</c> nghĩa là buổi diễn đã đóng mà chưa từng được
/// đánh dấu bắt đầu — có thể nó đã không diễn ra, và khi đó người mua vé cần được hoàn tiền.
/// <c>Measured</c> nghĩa là có diễn ra nhưng ngắn hơn dự kiến, xem <c>Ratio</c>.
/// </param>
/// <param name="Ratio">Chỉ có giá trị khi <c>Verdict</c> là <c>Measured</c>.</param>
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
    string Verdict,
    decimal? Ratio,
    decimal Threshold,
    bool HasPendingRefund);
