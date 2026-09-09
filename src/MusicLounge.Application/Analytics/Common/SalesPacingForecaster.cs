namespace MusicLounge.Application.Analytics.Common;

/// <summary>Kết quả dự báo, hoặc lý do không dự báo được.</summary>
public enum ForecastStatus
{
    /// <summary>Đủ căn cứ, có con số.</summary>
    Forecast,

    /// <summary>Chưa đủ buổi diễn đã hoàn tất để dựng đường bán vé tham chiếu.</summary>
    NotEnoughHistory,

    /// <summary>
    /// Còn quá xa ngày diễn: ở mốc này lịch sử cho thấy hầu như chưa ai mua, nên chia cho một tỉ lệ
    /// gần 0 sẽ thổi con số lên vô nghĩa.
    /// </summary>
    TooEarly
}

/// <param name="FinalSales">Tổng vé bán được của buổi diễn đã hoàn tất.</param>
/// <param name="SalesAtLeadTime">
/// Số vé đã bán tính tới mốc "còn N ngày nữa tới giờ diễn", với N là mốc của buổi diễn đang xét.
/// </param>
public sealed record ReferenceShowPace(int FinalSales, int SalesAtLeadTime);

public sealed record PacingForecast(
    ForecastStatus Status,
    int? ProjectedFinalSales,
    int? ProjectedLow,
    int? ProjectedHigh,
    decimal? ExpectedPaceFraction,
    decimal VenueHistoryWeight);

/// <summary>
/// Dự báo tổng vé bán ra của một buổi diễn đang mở bán, từ tốc độ bán hiện tại.
///
/// <b>Phương pháp lấy đúng cách ngành đang làm.</b> Nền tảng bán vé sự kiện dự báo lượng khách bằng
/// "đường bán vé" (sales pacing): nhìn lại các sự kiện đã xong để biết tới mốc còn N ngày thì
/// thường đã bán được bao nhiêu phần trăm tổng số, rồi áp tỉ lệ đó vào doanh số hiện tại của sự
/// kiện đang bán. Không phải mô hình hộp đen — đó là lý do nó giải thích được cho chủ phòng trà.
///
/// <b>Vì sao không dùng mạng nơ-ron.</b> Một phòng trà có vài chục buổi diễn, không phải vài triệu.
/// Mô hình nhiều tham số trên cỡ dữ liệu đó chỉ học thuộc nhiễu rồi trình bày nó như dự báo. Với
/// mẫu nhỏ, mô hình đơn giản và minh bạch cho kết quả tốt hơn và quan trọng hơn là kiểm chứng được.
///
/// <b>Xử lý mẫu nhỏ.</b> Một phòng trà mới có rất ít buổi diễn đã xong, nên đường bán vé riêng của
/// nó không đáng tin. Kéo nó về đường bán vé chung của cả nền tảng theo trọng số n/(n+K) — đây là
/// dạng chuẩn của empirical Bayes shrinkage: nhóm ít dữ liệu bị kéo mạnh về trung bình chung, nhóm
/// nhiều dữ liệu giữ được số của mình.
///
/// <b>Và biết lúc nào phải im lặng.</b> Dưới <see cref="MinimumReferenceShows"/> buổi diễn tham
/// chiếu thì không trả về con số nào. Đây không phải sự thận trọng tuỳ hứng: hiệu chỉnh kiểu này
/// mất hiệu lực khi có quá ít nhóm để ước lượng tham số. Một dự báo sai được trình bày như dự báo
/// đúng thì tệ hơn hẳn việc nói thẳng là chưa đủ dữ liệu — chủ phòng trà tin theo mà xếp lịch thì
/// thiệt hại là thật.
/// </summary>
public static class SalesPacingForecaster
{
    /// <summary>
    /// Số buổi diễn đã hoàn tất tối thiểu để dám đưa ra con số. Ngưỡng 3 là chỗ mà phép hiệu chỉnh
    /// mẫu nhỏ bắt đầu còn ý nghĩa — dưới mức đó thì chính tham số hiệu chỉnh cũng không ước lượng
    /// nổi, và kết quả không còn hiệu chuẩn.
    /// </summary>
    public const int MinimumReferenceShows = 3;

    /// <summary>
    /// Hằng số hiệu chỉnh. Đặt bằng <see cref="MinimumReferenceShows"/>: khi phòng trà vừa đủ số
    /// buổi diễn tối thiểu, lịch sử riêng của nó và lịch sử chung của nền tảng có tiếng nói ngang
    /// nhau; càng nhiều buổi diễn thì tiếng nói riêng càng lấn.
    ///
    /// Để trong code chứ không đưa ra system_config: đây là tham số của mô hình, không phải một
    /// ngưỡng vận hành. Chỉnh nó là đổi cách mô hình hoạt động, phải đi kèm lập luận, không phải
    /// việc sửa nhanh trên màn hình cấu hình.
    /// </summary>
    public const int ShrinkageConstant = MinimumReferenceShows;

    /// <summary>
    /// Dưới mốc này thì tỉ lệ bán kỳ vọng quá nhỏ để chia. Ví dụ lịch sử cho thấy tới mốc hiện tại
    /// thường mới bán được 2% tổng vé: chia doanh số hiện tại cho 0.02 là nhân nó lên 50 lần, và
    /// sai số của tử số cũng được nhân lên 50 lần theo.
    /// </summary>
    public const decimal MinimumUsablePaceFraction = 0.05m;

    /// <param name="venueHistory">Buổi diễn đã hoàn tất của chính phòng trà này.</param>
    /// <param name="platformHistory">
    /// Buổi diễn đã hoàn tất của cả nền tảng (bao gồm cả của phòng trà này) — nền để kéo về khi
    /// lịch sử riêng còn mỏng.
    /// </param>
    /// <param name="ticketsSoldSoFar">Số vé buổi diễn đang xét đã bán được tới lúc này.</param>
    /// <param name="capacity">Sức chứa, nếu có. Dự báo không vượt quá số ghế thật sự tồn tại.</param>
    public static PacingForecast Forecast(
        IReadOnlyList<ReferenceShowPace> venueHistory,
        IReadOnlyList<ReferenceShowPace> platformHistory,
        int ticketsSoldSoFar,
        int? capacity)
    {
        var venueUsable = Usable(venueHistory);
        var platformUsable = Usable(platformHistory);

        if (platformUsable.Count < MinimumReferenceShows)
            return new PacingForecast(ForecastStatus.NotEnoughHistory, null, null, null, null, 0m);

        // n/(n+K): phòng trà chưa có buổi diễn nào đã xong thì trọng số 0 — dùng hoàn toàn đường
        // bán vé chung; càng nhiều buổi diễn riêng thì càng nghiêng về số của chính mình.
        var n = venueUsable.Count;
        var weight = (decimal)n / (n + ShrinkageConstant);

        var venuePace = venueUsable.Count > 0 ? MeanPace(venueUsable) : 0m;
        var platformPace = MeanPace(platformUsable);
        var pace = weight * venuePace + (1 - weight) * platformPace;

        if (pace < MinimumUsablePaceFraction)
            return new PacingForecast(ForecastStatus.TooEarly, null, null, null, pace, weight);

        var projected = (int)Math.Round(ticketsSoldSoFar / pace, MidpointRounding.AwayFromZero);

        // Khoảng tin cậy lấy từ chính độ tản của các buổi diễn tham chiếu, không phải một con số
        // ước chừng: mỗi buổi diễn cũ cho một tỉ lệ, mỗi tỉ lệ cho một dự báo, và bề rộng của chùm
        // dự báo đó chính là mức không chắc chắn thật sự của phương pháp này trên dữ liệu này.
        var reference = venueUsable.Count >= MinimumReferenceShows ? venueUsable : platformUsable;
        var projections = reference
            .Select(r => (decimal)r.SalesAtLeadTime / r.FinalSales)
            .Where(f => f >= MinimumUsablePaceFraction)
            .Select(f => ticketsSoldSoFar / f)
            .OrderBy(v => v)
            .ToList();

        int? low = null, high = null;
        if (projections.Count > 0)
        {
            low = (int)Math.Round(projections[0], MidpointRounding.AwayFromZero);
            high = (int)Math.Round(projections[^1], MidpointRounding.AwayFromZero);
        }

        // Không bao giờ dự báo ít hơn số vé đã bán rồi, và không bao giờ nhiều hơn số ghế có thật.
        projected = Clamp(projected, ticketsSoldSoFar, capacity);
        low = low is null ? null : Clamp(low.Value, ticketsSoldSoFar, capacity);
        high = high is null ? null : Clamp(high.Value, ticketsSoldSoFar, capacity);

        return new PacingForecast(ForecastStatus.Forecast, projected, low, high, pace, weight);
    }

    /// <summary>
    /// Buổi diễn không bán được vé nào thì không nói lên điều gì về nhịp bán — chia cho 0. Loại
    /// khỏi tập tham chiếu thay vì để nó kéo trung bình về những chỗ vô nghĩa.
    /// </summary>
    private static List<ReferenceShowPace> Usable(IReadOnlyList<ReferenceShowPace> history)
        => history.Where(h => h.FinalSales > 0).ToList();

    private static decimal MeanPace(List<ReferenceShowPace> history)
        => history.Average(h => (decimal)h.SalesAtLeadTime / h.FinalSales);

    private static int Clamp(int value, int floor, int? ceiling)
    {
        var result = Math.Max(value, floor);
        return ceiling is int cap ? Math.Min(result, cap) : result;
    }
}
