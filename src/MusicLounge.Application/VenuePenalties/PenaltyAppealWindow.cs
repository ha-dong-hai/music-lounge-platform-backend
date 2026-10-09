using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.VenuePenalties;

/// <summary>
/// MLACP-702. Khoảng thời gian chủ phòng trà được phép gửi khiếu nại, tính từ lúc án phạt được ban hành.
/// Một nguồn duy nhất cho hai nơi hỏi: lệnh gửi khiếu nại (chặn quá hạn) và danh sách án phạt của chủ
/// phòng trà (để web biết khi nào còn hiện nút).
///
/// <para>Trước task này web chỉ hiện nút khi <c>AppealDeadline</c> còn hạn, nhưng trường đó chỉ được gán
/// SAU khi khiếu nại (đó là hạn SLA của Admin) — nên nút không bao giờ hiện. Hạn thật phải tính từ
/// <c>IssuedAt</c> cộng số ngày này, nên nay cùng một hàm trả hạn cho cả hai.</para>
///
/// <para><c>penalty_appeal_window_days</c> KHÔNG được seed vào <c>system_config</c>, nên mặc định ở đây
/// chính là chính sách đang chạy. Hạn không lưu thành cột: đổi cấu hình là áp ngay cho cả các án đang
/// mở.</para>
/// </summary>
public static class PenaltyAppealWindow
{
    public const int DefaultDays = 7;

    public static Task<int> DaysAsync(ISystemConfigService config, CancellationToken ct)
        => config.GetIntAsync(ConfigKeys.PenaltyAppealWindowDays, DefaultDays, ct);

    /// <summary>Hạn cuối để gửi khiếu nại, tính từ lúc án phạt được ban hành.</summary>
    public static DateTimeOffset EndsAt(DateTimeOffset issuedAt, int windowDays)
        => issuedAt.AddDays(windowDays);
}
