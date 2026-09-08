using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Lịch diễn của một buổi diễn chiếm chỗ ở phòng trà từ lúc nào đến lúc nào.
///
/// <see cref="LoungeShow.ScheduledEnd"/> cho phép null, và toàn hệ thống đã thống nhất cách hiểu
/// từ lâu: không khai giờ kết thúc thì tính 4 tiếng. Biểu thức đó đang được viết lại y hệt ở sáu
/// chỗ — hoàn tiền vé, huỷ vé, xếp lịch đối soát, job tự kết thúc buổi diễn quá hạn, job giải ngân.
/// Gom về đây trước khi thêm chỗ thứ bảy, vì chỗ thứ bảy này là bộ chống trùng lịch: nếu nó hiểu
/// "giờ kết thúc" khác với phần còn lại của hệ thống thì nó vừa chặn nhầm vừa bỏ lọt.
/// </summary>
public static class ShowSchedule
{
    /// <summary>
    /// Độ dài mặc định khi buổi diễn không khai giờ kết thúc. Con số 4 tiếng không phải tôi chọn
    /// mới — nó là giá trị đã chạy sẵn ở cả sáu chỗ nói trên, và đổi nó sẽ đổi luôn hạn hoàn tiền
    /// lẫn thời điểm đối soát của mọi buổi diễn đang mở bán.
    /// </summary>
    public const int DefaultDurationHours = 4;

    public static DateTimeOffset EffectiveEnd(DateTimeOffset start, DateTimeOffset? end)
        => end ?? start.AddHours(DefaultDurationHours);

    public static DateTimeOffset EffectiveEnd(LoungeShow show)
        => EffectiveEnd(show.ScheduledStart, show.ScheduledEnd);

    /// <summary>
    /// Các trạng thái mà buổi diễn thật sự đang giữ chỗ ở phòng trà.
    ///
    /// <see cref="LoungeShowStatus.Draft"/> cố ý không nằm đây: bản nháp là chỗ Owner dựng thử, và
    /// dựng hai phương án cho cùng một buổi tối là việc bình thường. Chỉ khi nộp duyệt thì buổi
    /// diễn mới thật sự đặt chỗ.
    ///
    /// <see cref="LoungeShowStatus.Ended"/> và <see cref="LoungeShowStatus.Cancelled"/> cũng không
    /// nằm đây — chúng đã trả chỗ lại rồi.
    /// </summary>
    public static readonly LoungeShowStatus[] OccupiesTheVenue =
    [
        LoungeShowStatus.Pending,
        LoungeShowStatus.Published,
        LoungeShowStatus.Ongoing
    ];

    /// <summary>
    /// Hai khoảng thời gian có giẫm lên nhau không. Dùng so sánh chặt ở cả hai đầu, nên hai buổi
    /// diễn nối đuôi nhau — buổi này kết thúc đúng lúc buổi kia bắt đầu — KHÔNG bị tính là trùng.
    /// Đó là cách xếp lịch bình thường của một phòng trà chạy hai suất một tối.
    /// </summary>
    public static bool Overlaps(
        DateTimeOffset startA, DateTimeOffset endA,
        DateTimeOffset startB, DateTimeOffset endB)
        => startA < endB && startB < endA;
}
