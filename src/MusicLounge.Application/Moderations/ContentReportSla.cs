using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Moderations;

/// <summary>
/// MLACP-446. Thời hạn Admin phải xử lý xong một báo cáo nội dung của người dùng. Một nguồn duy nhất
/// cho hàng đợi báo cáo (nơi hiển thị hạn) và job cảnh báo quá hạn — trước đây mỗi nơi ghi thẳng số 48.
///
/// <para><c>content_report_sla_hours</c> KHÔNG được seed, nên con số ở đây chính là mốc đang áp thật.
/// Hai chỗ lệch nhau nghĩa là hàng đợi báo một đằng còn cảnh báo gửi Admin một nẻo.</para>
///
/// <para>48 giờ theo MLACP-222: NĐ 147/2024/NĐ-CP đặt mốc 48h cho yêu cầu gỡ nội dung đến từ NGƯỜI
/// DÙNG — đúng trường hợp báo cáo của khán giả ở đây. Mốc 24h của nghị định chỉ áp khi yêu cầu đến từ
/// cơ quan quản lý nhà nước có thẩm quyền, nên không dùng ở đây (xem <c>moderation_sla_hours</c> cho
/// cổng duyệt AI trước khi đăng — một mốc khác, cho một việc khác).</para>
/// </summary>
public static class ContentReportSla
{
    public const int DefaultSlaHours = 48;

    public static Task<int> SlaHoursAsync(ISystemConfigService config, CancellationToken ct)
        => config.GetIntAsync(ConfigKeys.ContentReportSlaHours, DefaultSlaHours, ct);
}
