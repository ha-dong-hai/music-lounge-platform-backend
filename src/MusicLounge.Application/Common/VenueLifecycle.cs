using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

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
    /// MLACP-354. Câu từ chối cho <b>người mua</b> khi phòng trà không được phép giao dịch.
    ///
    /// <para>Không dùng <see cref="ExplainRestriction"/> cho người mua: câu đó viết cho chủ phòng trà
    /// ("vui lòng chỉnh sửa hồ sơ và liên hệ Admin"), và lý do phòng trà bị đình chỉ là chuyện giữa
    /// phòng trà với nền tảng — người mua chỉ cần biết là chưa mua được.</para>
    /// </summary>
    public const string TradingPausedForBuyers =
        "Phòng trà của buổi diễn này hiện tạm ngừng giao dịch trên nền tảng — chưa thể mua vé hay donate lúc này.";

    /// <summary>
    /// MLACP-354. Trạng thái hiện tại của phòng trà, đọc thẳng từ cơ sở dữ liệu. Null khi không tìm
    /// thấy — nơi gọi coi đó là không được phép giao dịch.
    ///
    /// <para><see cref="CanOperate"/> đã ghi rõ nó quyết "bán vé, nhận donation", nhưng trước task này
    /// chỉ cổng nộp duyệt buổi diễn và các danh sách công khai dùng tới nó. Một phòng trà đang bị tạm
    /// đình chỉ hay khoá vĩnh viễn thì bị ẩn khỏi danh sách — nhưng ai có đường dẫn tới buổi diễn vẫn
    /// giữ chỗ, trả tiền và donate được.</para>
    /// </summary>
    public static async Task<LoungeStatus?> StatusOfAsync(IUnitOfWork uow, int loungeId, CancellationToken ct)
        => (await uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(loungeId, ct))?.Status;

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
