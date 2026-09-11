using MusicLounge.Domain.Entities;
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

    /// <summary>Trạng thái mà một loại án phạt đặt lên phòng trà khi có hiệu lực.</summary>
    public static LoungeStatus StatusImposedBy(PenaltyType type) => type switch
    {
        PenaltyType.Warning => LoungeStatus.Warned,
        PenaltyType.Suspension => LoungeStatus.Suspended,
        PenaltyType.Ban => LoungeStatus.Locked,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>
    /// MLACP-367 — áp một án phạt: trạng thái phòng trà chỉ được nặng thêm, không bao giờ nhẹ đi. Null: giữ
    /// nguyên.
    ///
    /// <para>Trước đây mỗi chỗ tự gán thẳng. Ra lệnh cảnh cáo đặt Warned kể cả khi phòng trà đang bị khoá —
    /// mà Warned vẫn được hoạt động (<see cref="VenueLifecycle.Operating"/>), nên một lời cảnh cáo mở khoá
    /// phòng trà. Lệnh tạm khoá có hiệu lực sau lệnh khoá vĩnh viễn hạ Locked xuống Suspended, rồi hết hạn
    /// tạm khoá là phòng trà được mở luôn.</para>
    ///
    /// <para>Phòng trà chưa được duyệt hồ sơ (Pending/Rejected) vốn không được hoạt động: cảnh cáo không đổi
    /// gì — đặt Warned là cho nó hoạt động; tạm khoá/khoá vĩnh viễn vẫn đặt như trước.</para>
    /// </summary>
    public static LoungeStatus? StatusAfterImposing(LoungeStatus current, PenaltyType type)
    {
        var imposed = StatusImposedBy(type);
        if (Severity(current) is null)
            return type == PenaltyType.Warning ? null : imposed;
        return Severity(imposed) > Severity(current) ? imposed : null;
    }

    /// <summary>
    /// MLACP-367 — một án phạt thôi hiệu lực (kháng cáo được chấp thuận, tạm khoá hết hạn): trạng thái suy từ
    /// các án CÒN hiệu lực, và chỉ được nhẹ đi. Null: giữ nguyên.
    ///
    /// <para>Không đụng vào trạng thái nặng hơn mức án được gỡ từng đặt ra: thứ gì khác đã đặt nó ở đó (một
    /// án khác, hoặc Admin) và quyết định đó thắng. Không đụng vào phòng trà chưa được duyệt hồ sơ.</para>
    /// </summary>
    public static LoungeStatus? StatusAfterReleasing(
        LoungeStatus current, PenaltyType released, IEnumerable<VenuePenalty> remainingInForce)
    {
        if (Severity(current) is not int now || now > Severity(StatusImposedBy(released)))
            return null;
        var remaining = StatusFrom(remainingInForce);
        return Severity(remaining) < now ? remaining : null;
    }

    /// <summary>Câu mô tả trạng thái sau cùng cho chủ phòng trà — chỉ nói điều đúng với trạng thái đó.</summary>
    public static string DescribeForOwner(LoungeStatus status) => status switch
    {
        LoungeStatus.Approved => "Phòng trà hoạt động bình thường.",
        LoungeStatus.Warned => "Phòng trà hoạt động bình thường; vẫn còn cảnh cáo đang có hiệu lực.",
        LoungeStatus.Suspended => "Phòng trà vẫn đang bị tạm khoá.",
        LoungeStatus.Locked => "Phòng trà vẫn đang bị khoá.",
        _ => ""
    };

    /// <summary>
    /// Án nặng nhất còn ràng buộc. Tạm khoá/khoá vĩnh viễn chỉ tính khi đã được áp (AppliedAt) — trước đó
    /// là thời gian báo trước, phòng trà vẫn hoạt động. Cảnh cáo có hiệu lực ngay khi ra lệnh.
    /// </summary>
    private static LoungeStatus StatusFrom(IEnumerable<VenuePenalty> penalties)
        => penalties
            .Where(p => InForce.Contains(p.Status)
                        && (p.PenaltyType == PenaltyType.Warning || p.AppliedAt is not null))
            .Select(p => StatusImposedBy(p.PenaltyType))
            .Append(LoungeStatus.Approved)
            .OrderByDescending(Severity)
            .First();

    private static int? Severity(LoungeStatus status) => status switch
    {
        LoungeStatus.Approved => 0,
        LoungeStatus.Warned => 1,
        LoungeStatus.Suspended => 2,
        LoungeStatus.Locked => 3,
        _ => null
    };
}
