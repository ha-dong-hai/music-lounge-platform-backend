using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Refunds;

/// <summary>
/// MLACP-446. Thời hạn Admin phải xử lý xong một yêu cầu hoàn tiền, và khoảng ân hạn sau đó trước khi
/// hệ thống tự duyệt. Một nguồn duy nhất cho cả bốn nơi hỏi: hai màn hình danh sách yêu cầu (hạn hiển
/// thị cho người mua và cho Admin), job cảnh báo quá hạn, và job tự duyệt.
///
/// <para><c>refund_sla_hours</c> KHÔNG được seed vào <c>system_config</c>, nên giá trị mặc định ở đây
/// chính là chính sách đang chạy thật — không phải một con số dự phòng hiếm khi dùng tới. Trước task
/// này nó được viết lại bốn lần: hai chỗ ghi thẳng số 72, hai chỗ dùng hai hằng <c>DefaultSlaHours</c>
/// riêng của hai lớp job khác nhau. Chú thích trong <c>GetMyRefundRequestsQueryHandler</c> tự khẳng
/// định bốn chỗ đó "phải là cùng một con số, nếu không thì một bên sẽ im lặng trong khi bên kia đã trễ
/// hạn" — mà không có gì bắt buộc điều đó. Ai sửa một chỗ quên ba chỗ kia thì hạn hứa với người mua
/// khác hạn hệ thống thực sự áp, và không test nào đỏ.</para>
///
/// <para>72 giờ = 3 ngày làm việc, khớp Điều 31 Luật Bảo vệ quyền lợi người tiêu dùng 2023 (thông báo
/// tiếp nhận trong 03 ngày làm việc) và chặt hơn mức 5 ngày làm việc Eventbrite cam kết cho ban tổ
/// chức. Hạn được tính từ <c>RefundRequest.CreatedAt</c> cộng số giờ này chứ không lưu thành cột, nên
/// đổi cấu hình là áp ngay cho cả các yêu cầu đang chờ.</para>
/// </summary>
public static class RefundSla
{
    public const int DefaultSlaHours = 72;

    /// <summary>
    /// MLACP-348: sau khi quá <see cref="DefaultSlaHours"/> thì còn chừng này giờ nữa Admin mới mất
    /// quyền từ chối — đây là khoảng để từ chối một yêu cầu đáng ngờ trước khi hệ thống tự duyệt.
    /// </summary>
    public const int DefaultAutoApproveGraceHours = 24;

    public static Task<int> SlaHoursAsync(ISystemConfigService config, CancellationToken ct)
        => config.GetIntAsync(ConfigKeys.RefundSlaHours, DefaultSlaHours, ct);

    public static Task<int> AutoApproveGraceHoursAsync(ISystemConfigService config, CancellationToken ct)
        => config.GetIntAsync(ConfigKeys.RefundAutoApproveGraceHours, DefaultAutoApproveGraceHours, ct);

    /// <summary>Hạn Admin phải xử lý xong, tính từ lúc yêu cầu được tạo.</summary>
    public static DateTimeOffset Deadline(DateTimeOffset createdAt, int slaHours)
        => createdAt.AddHours(slaHours);
}
