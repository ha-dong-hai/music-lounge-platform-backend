namespace MusicLounge.Application.Analytics.DTOs;

/// <param name="Status">
/// 'Forecast' khi có con số; 'NotEnoughHistory' hoặc 'TooEarly' khi không. Hai trạng thái sau
/// không phải lỗi — chúng là câu trả lời đúng cho câu hỏi "dự báo được chưa".
/// </param>
/// <param name="Explanation">
/// Câu giải thích cho chủ phòng trà đọc: dựa trên bao nhiêu buổi diễn, ở mốc nào, và vì sao không
/// có số nếu không có. Một dự báo không nói được nó dựa trên cái gì thì không dùng để quyết định gì.
/// </param>
/// <param name="ExpectedPaceFraction">
/// Tỉ lệ tổng vé mà lịch sử cho thấy thường đã bán được tính tới mốc còn từng này ngày. Đây chính
/// là con số dùng để suy ra dự báo — đưa ra để kiểm chứng được.
/// </param>
/// <param name="VenueHistoryWeight">
/// Dự báo nghiêng về lịch sử của chính phòng trà này bao nhiêu (0 = hoàn toàn dùng dữ liệu chung
/// của nền tảng, 1 = hoàn toàn dùng dữ liệu riêng). Phòng trà càng nhiều buổi diễn đã xong thì
/// con số này càng cao.
/// </param>
public sealed record DemandForecastDto(
    int ShowId,
    string ShowName,
    string Status,
    string Explanation,
    int DaysUntilShow,
    int TicketsSoldSoFar,
    int? ProjectedFinalSales,
    int? ProjectedLow,
    int? ProjectedHigh,
    decimal? ExpectedPaceFraction,
    decimal VenueHistoryWeight,
    int VenueReferenceShows,
    int PlatformReferenceShows,
    int? Capacity,
    decimal? ProjectedSellThroughRate);
