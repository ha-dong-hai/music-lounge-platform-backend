using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Analytics.Common;

/// <summary>Một tín hiệu quan tâm tới buổi diễn, đã quy về thang chung của bảng xếp hạng.</summary>
public enum TrendingSignal
{
    /// <summary>Mở trang buổi diễn, xem danh sách nghệ sĩ, xem trang phòng trà, tìm theo thể loại.</summary>
    Browse,

    /// <summary>Ở lại trang đủ lâu, hoặc xem livestream — đọc thật chứ không lướt qua.</summary>
    Attention,

    /// <summary>Lưu vào danh sách quan tâm.</summary>
    Save,

    /// <summary>Chia sẻ cho người khác.</summary>
    Share,

    /// <summary>Bấm vào nút mua vé.</summary>
    Intent,

    /// <summary>Mua vé thật.</summary>
    Purchase
}

/// <param name="Actor">
/// Ai (hoặc cái gì) tạo ra tín hiệu này. Hai tín hiệu cùng Actor, cùng loại, cùng ngày chỉ tính
/// một lần — đó là cách chống thổi số bằng cách bấm lại.
///
/// Với lượt xem và lượt lưu thì Actor là người dùng. Với vé đã bán thì Actor là chính chiếc vé,
/// nên một người mua bốn vé được tính là bốn tín hiệu: đặt bốn chỗ cho cả nhóm là mức quan tâm
/// khác hẳn mua một vé đi một mình, và gộp chúng lại sẽ xoá mất đúng phần khác biệt đó.
/// </param>
public sealed record TrendingEvent(string Actor, TrendingSignal Signal, DateTimeOffset At);

/// <summary>
/// Chấm điểm "đang được quan tâm" cho buổi diễn.
///
/// <b>Cách cũ và vì sao phải đổi.</b> Bảng xếp hạng trước đây là: đếm số bản ghi hành vi trong 7
/// ngày, không phân biệt loại, cắt cứng ở mốc 7 ngày. Ba hệ quả:
///
/// Một lượt xem lướt nặng ngang một vé đã mua, nên buổi diễn được 50 người liếc qua xếp trên buổi
/// diễn 10 người thật sự mua vé. Một người bấm F5 năm mươi lần tạo ra năm mươi điểm. Và vì mốc 7
/// ngày là ranh giới cứng, buổi diễn nóng tuần trước nhưng nay đã nguội vẫn xếp ngang buổi diễn
/// đang lên — tức bảng đó đo "được chú ý nhiều", không phải "đang được chú ý".
///
/// <b>Ba thay đổi, mỗi cái sửa đúng một điểm trên.</b>
///
/// 1. <b>Trọng số theo mức độ gần với hành vi mua.</b> Mua vé nặng nhất, rồi tới bấm nút mua, lưu
///    lại, chia sẻ, đọc kỹ, và cuối cùng là lướt qua. Đây là thang thứ tự do thiết kế, không phải
///    rút ra từ dữ liệu — nguyên tắc sắp thứ tự là: hành động càng tốn công và càng gần lúc rút ví
///    thì càng nói lên sự quan tâm thật.
///
/// 2. <b>Suy giảm hàm mũ theo nửa chu kỳ thay cho cắt cứng.</b> Mỗi tín hiệu mất một nửa trọng số
///    sau <see cref="HalfLifeHours"/> giờ. Không còn vách đứng giữa "6 ngày 23 giờ" và "7 ngày 1
///    giờ", và sự quan tâm cũ mờ dần thay vì biến mất đột ngột. Đây là cách các bảng xếp hạng thật
///    làm; Hacker News dùng dạng luỹ thừa của thời gian, còn dạng hàm mũ theo nửa chu kỳ dễ giải
///    thích hơn và có một tham số duy nhất mang nghĩa rõ ràng.
///
/// 3. <b>Gộp trùng theo người và theo ngày.</b> Một người, một loại tín hiệu, một ngày thì tính một
///    lần. Không có bước này thì bảng xếp hạng đo được ai bấm F5 nhiều nhất.
///
/// <b>Nguồn tín hiệu không được phụ thuộc vào việc bật AI.</b> Nhật ký hành vi chỉ được ghi cho người
/// đã đồng ý cho dùng dữ liệu cá nhân (<c>AiConsent</c>), mà mặc định là tắt — nên nếu bảng xếp
/// hạng chỉ đọc nhật ký đó thì nó gần như luôn rỗng, đúng tình trạng hiện nay. Vé đã bán và lượt
/// lưu vào danh sách quan tâm là giao dịch của chính người dùng, được lưu để phục vụ họ chứ không
/// phải để phân tích hành vi, nên chúng tồn tại bất kể consent — và chúng cũng là hai tín hiệu
/// mạnh nhất. Đếm tổng hợp trên chúng không phải là lập hồ sơ người dùng.
///
/// <b>Vì sao không cần hiệu chỉnh mẫu nhỏ ở đây</b>, khác với
/// <see cref="SalesPacingForecaster"/>: điểm này là một TỔNG có trọng số, không phải trung bình hay
/// tỉ lệ. Vấn đề "2 lượt thích trên 2 lượt xếp trên 100 trên 101" chỉ phát sinh khi chia. Buổi diễn
/// có hai tín hiệu thì tổng của nó nhỏ, và nó xếp thấp — đúng như phải thế.
/// </summary>
public static class TrendingScorer
{
    /// <summary>
    /// Sau bấy nhiêu giờ, một tín hiệu còn lại một nửa trọng số. Ba ngày: sau một tuần còn khoảng
    /// 19%, sau hai tuần còn khoảng 3,5%.
    ///
    /// Chọn dài hơn hẳn các bảng tin tức (thường tính bằng giờ) vì đây là hai loại hành vi khác
    /// nhau: một bản tin sống được vài giờ, còn quyết định đi xem một buổi diễn được cân nhắc trong
    /// nhiều ngày và vé thường mở bán trước hàng tuần. Lấy nửa chu kỳ vài giờ ở đây sẽ khiến bảng
    /// xếp hạng chỉ phản ánh lưu lượng đêm qua.
    /// </summary>
    public const double HalfLifeHours = 72;

    /// <summary>
    /// Thang trọng số. Khoảng cách giữa các bậc cố ý rộng: một vé bán được đáng giá bằng mười lượt
    /// xem, vì nó là bằng chứng duy nhất mà người dùng phải trả tiền để tạo ra.
    /// </summary>
    public static double WeightOf(TrendingSignal signal) => signal switch
    {
        TrendingSignal.Purchase => 10,
        TrendingSignal.Intent => 4,
        TrendingSignal.Save => 3,
        TrendingSignal.Share => 3,
        TrendingSignal.Attention => 2,
        TrendingSignal.Browse => 1,
        _ => 0
    };

    /// <summary>
    /// Quy hành động ghi trong nhật ký về thang tín hiệu chung. Gộp lại vì bảng xếp hạng không cần
    /// biết người dùng xem danh sách nghệ sĩ hay xem trang phòng trà — cả hai đều là "đang xem".
    /// </summary>
    public static TrendingSignal? FromBehaviour(BehaviourAction action) => action switch
    {
        BehaviourAction.PurchaseTicket => TrendingSignal.Purchase,
        BehaviourAction.ClickTicket => TrendingSignal.Intent,
        BehaviourAction.ShareEvent => TrendingSignal.Share,
        BehaviourAction.WatchLivestream => TrendingSignal.Attention,
        BehaviourAction.ViewEventLong => TrendingSignal.Attention,
        BehaviourAction.ViewAfterWishlist => TrendingSignal.Attention,
        BehaviourAction.ViewEvent => TrendingSignal.Browse,
        BehaviourAction.ViewLineup => TrendingSignal.Browse,
        BehaviourAction.ViewVenue => TrendingSignal.Browse,
        BehaviourAction.SearchGenre => TrendingSignal.Browse,
        _ => null
    };

    /// <summary>
    /// Điểm quan tâm tại thời điểm <paramref name="now"/>. Càng cao càng đang được chú ý.
    /// Không có tín hiệu nào thì bằng 0 — và 0 là một câu trả lời hợp lệ, không phải lỗi.
    /// </summary>
    public static double Score(IEnumerable<TrendingEvent> events, DateTimeOffset now)
    {
        double score = 0;

        foreach (var e in Deduplicate(events))
        {
            var ageHours = (now - e.At).TotalHours;

            // Tín hiệu ghi ở tương lai (lệch đồng hồ) không được nhân trọng số lên; coi như vừa xảy ra.
            if (ageHours < 0) ageHours = 0;

            score += WeightOf(e.Signal) * Math.Pow(0.5, ageHours / HalfLifeHours);
        }

        return score;
    }

    /// <summary>
    /// Một người, một loại tín hiệu, một ngày thì tính một lần — giữ lại lần sớm nhất trong ngày,
    /// vì đó là lúc sự quan tâm thật sự xuất hiện.
    ///
    /// Gộp theo ngày chứ không theo cả kỳ: người quay lại xem tiếp vào hôm sau là tín hiệu thật và
    /// đáng được tính thêm, còn người mở đi mở lại trong cùng một buổi tối thì không.
    /// </summary>
    private static IEnumerable<TrendingEvent> Deduplicate(IEnumerable<TrendingEvent> events)
        => events
            .GroupBy(e => (e.Actor, e.Signal, e.At.UtcDateTime.Date))
            .Select(g => g.OrderBy(e => e.At).First());
}
