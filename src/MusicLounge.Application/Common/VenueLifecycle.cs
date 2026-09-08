using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Một phòng trà "đang được phép hoạt động" nghĩa là gì.
///
/// Trước MLACP-307 câu hỏi này có ba câu trả lời khác nhau trong cùng một hệ thống. Cổng nộp duyệt
/// buổi diễn chặn <c>Suspended</c>/<c>Locked</c> và cho <c>Pending</c> đi qua. Danh sách phòng trà
/// công khai không lọc gì cả. Bảng tổng quan của Admin đếm <c>Approved</c> + <c>Warned</c>. Chính
/// sự bất đồng đó là lỗi: một phòng trà chưa ai duyệt vừa nằm trong danh sách công khai, vừa mở
/// bán vé và thu tiền thật.
///
/// Gộp về một chỗ vì đây là câu hỏi sẽ còn được hỏi lại mỗi lần thêm một luồng mới, và ba lần trả
/// lời độc lập đã cho ba đáp án.
/// </summary>
public static class VenueLifecycle
{
    /// <summary>
    /// Các trạng thái mà phòng trà vẫn giao dịch bình thường.
    ///
    /// <see cref="LoungeStatus.Warned"/> nằm trong đây. Cảnh cáo là một vết ghi lại, không phải
    /// lệnh dừng — venue bị cảnh cáo vẫn bán vé, đúng như <c>GetAdminPlatformOverview</c> vẫn đếm
    /// nó là venue đang hoạt động từ trước tới nay.
    ///
    /// Để dạng mảng chứ không phải hàm: nó được dùng thẳng trong truy vấn database qua Contains,
    /// và một lời gọi hàm thì không dịch được sang SQL.
    /// </summary>
    public static readonly LoungeStatus[] Operating =
    [
        LoungeStatus.Approved,
        LoungeStatus.Warned
    ];

    /// <summary>Phòng trà có được mở buổi diễn mới, bán vé, nhận donation hay không.</summary>
    public static bool CanOperate(LoungeStatus status) => Operating.Contains(status);

    /// <summary>
    /// Phòng trà có được hiện ra cho người ngoài hay không. Trùng với <see cref="CanOperate"/> hôm
    /// nay, nhưng tách tên vì hai câu hỏi này khác nhau về bản chất và có thể tách đôi về sau —
    /// ví dụ nếu muốn giữ trang của một venue đang bị đình chỉ để khán giả đã mua vé còn tra cứu.
    /// </summary>
    public static bool IsPubliclyVisible(LoungeStatus status) => CanOperate(status);

    /// <summary>
    /// Lý do bị chặn, viết cho Owner đọc. Tách riêng khỏi hành động bị chặn để mỗi nơi gọi tự ghép
    /// hành động của mình vào — "đang chờ duyệt" và "đã bị từ chối" là hai tình huống rất khác
    /// nhau đối với người nhận, gộp chung thành một câu "trạng thái không hợp lệ" thì họ không biết
    /// phải làm gì tiếp.
    /// </summary>
    public static string ExplainRestriction(LoungeStatus status) => status switch
    {
        LoungeStatus.Pending =>
            "Phòng trà đang chờ Admin duyệt hồ sơ.",
        LoungeStatus.Rejected =>
            "Hồ sơ phòng trà đã bị từ chối — vui lòng xem lý do trong thông báo, chỉnh sửa và liên hệ Admin để duyệt lại.",
        LoungeStatus.Suspended =>
            "Phòng trà đang bị tạm đình chỉ do vi phạm.",
        LoungeStatus.Locked =>
            "Phòng trà đang bị khoá do vi phạm.",
        _ =>
            $"Phòng trà đang ở trạng thái '{status}'."
    };
}
