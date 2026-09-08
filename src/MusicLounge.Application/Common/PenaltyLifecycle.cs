using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Một án phạt "đang có hiệu lực" nghĩa là gì.
///
/// Trước MLACP-304 câu hỏi này được trả lời lại ở bốn chỗ, và ba chỗ chỉ kiểm <c>Active</c>. Điều
/// đó bỏ sót <see cref="PenaltyStatus.Upheld"/> — trạng thái của một án phạt đã bị khiếu nại và
/// khiếu nại đó bị từ chối, tức là án phạt vẫn nguyên hiệu lực, chỉ khác là đã qua bước xét.
///
/// Hậu quả đi theo hai hướng ngược nhau, cùng từ một chỗ sót:
///
/// Job gỡ lệnh tạm khoá lọc <c>Active</c> nên bỏ qua hẳn án phạt <c>Upheld</c> — venue khiếu nại
/// rồi thua thì bị khoá vĩnh viễn, đúng cái lỗ MLACP-299 sinh ra để vá.
///
/// Ngược lại, phép đếm "còn án phạt nào khác không" cũng chỉ đếm <c>Active</c>, nên một venue đang
/// chịu án <c>Upheld</c> có thể được thả sớm khi một án khác được lật.
///
/// <see cref="PenaltyStatus.Appealed"/> cố ý KHÔNG nằm trong nhóm này khi xét gỡ hạn: khiếu nại
/// đang chờ thì để bước xét khiếu nại quyết, và AutoApproveOverdueAppealsJob đã chặn trường hợp
/// khiếu nại bị ngâm quá lâu.
/// </summary>
public static class PenaltyLifecycle
{
    /// <summary>
    /// Các trạng thái mà án phạt vẫn đang ràng buộc phòng trà.
    ///
    /// Để dạng mảng chứ không phải hàm: nó được dùng thẳng trong truy vấn database qua Contains,
    /// và một lời gọi hàm thì không dịch được sang SQL.
    /// </summary>
    public static readonly PenaltyStatus[] InForce =
    [
        PenaltyStatus.Active,
        PenaltyStatus.Upheld
    ];
}
