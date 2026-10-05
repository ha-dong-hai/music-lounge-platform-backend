using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.LoungeShows;

/// <summary>
/// MLACP-621. Đổi giờ diễn của một buổi đã được chấp thuận phải BÁO TRƯỚC bao nhiêu ngày làm việc.
///
/// <b>Căn cứ — NĐ 144/2020/NĐ-CP Điều 10 khoản 4 điểm đ (đọc nguyên văn 04/10/2026):</b> "Trường hợp thay đổi thời gian,
/// địa điểm tổ chức biểu diễn nghệ thuật đã được chấp thuận, tổ chức, cá nhân … có văn bản thông báo gửi … tới cơ quan
/// đã chấp thuận và chính quyền địa phương nơi tổ chức biểu diễn nghệ thuật ít nhất 02 ngày làm việc". Mốc 07 ngày làm
/// việc (điểm a, <c>publish_min_business_days_lead_time</c>) là cho hồ sơ xin chấp thuận LẦN ĐẦU, không phải cho việc dời
/// lịch một buổi đã được chấp thuận.
///
/// <b>Lỗi đã có:</b> đổi lịch dùng chung mốc 7 ngày với đăng mới, chặt hơn luật. Ca sĩ ốm, muốn dời 3 ngày thì hệ thống
/// chặn, phòng trà chỉ còn cách huỷ buổi diễn và hoàn tiền toàn bộ khách — mất doanh thu cho một việc luật cho phép.
/// Chú thích cũ lo "đăng đúng hạn rồi dời gấp" là lỗ hổng, nhưng chính luật đã quy định cách làm hợp lệ cho việc đó.
///
/// Khoá KHÔNG seed nên <see cref="DefaultBusinessDays"/> là chính sách đang chạy; đọc qua <see cref="BusinessDaysAsync"/>
/// để chỉ có một nguồn mặc định (UnseededConfigFallbackSingleSourceTests).
/// Trần giới hạn: hệ thống chỉ chặn ngày diễn mới quá gần; việc gửi văn bản thông báo cho cơ quan nhà nước vẫn do phòng
/// trà tự làm — nền tảng chưa lưu bằng chứng đã thông báo.
/// </summary>
public static class ShowRescheduleNotice
{
    public const int DefaultBusinessDays = 2;

    public static Task<int> BusinessDaysAsync(ISystemConfigService config, CancellationToken ct)
        => config.GetIntAsync(ConfigKeys.RescheduleMinBusinessDaysNotice, DefaultBusinessDays, ct);
}
