namespace MusicLounge.Application.Common;

/// <summary>
/// CF1, phần bổ sung sau khi rà lại quy tắc theo thực tế ngành (MLACP-310).
///
/// Bản đầu của bộ chống trùng lịch chỉ chặn hai buổi diễn giẫm lên nhau, và cho phép hai suất nối
/// đuôi nhau sát nhau tuyệt đối — suất sau bắt đầu đúng giây suất trước kết thúc. Điều đó không xảy
/// ra được ở một phòng trà thật: giữa hai suất còn phải đưa khán giả suất trước ra, dọn chỗ, chỉnh
/// lại sân khấu và nhận khán giả suất sau vào.
///
/// Khoảng cách tối thiểu 30 phút là đầu thấp của khoảng 30–60 phút mà các nhà hát/phòng diễn tiêu
/// chuẩn dùng để dọn một lượt khán giả và nhận lượt tiếp theo. Lấy đầu thấp để không siết quá tay
/// những phòng trà nhỏ, quay vòng nhanh.
///
/// Chuyện này không chỉ là xếp lịch cho gọn: nếu suất sau được bán vé với giờ bắt đầu mà thực tế
/// không thể bắt đầu đúng giờ, thì người chịu là khán giả đã mua vé suất đó.
/// </summary>
public static class VenueChangeover
{
    /// <summary>
    /// Mặc định 30 phút, đầu thấp của khoảng 30–60 phút theo thông lệ. Đọc từ system_config
    /// (<c>venue_changeover_minutes</c>) nên mỗi nền tảng chỉnh được theo quy mô phòng của mình.
    /// </summary>
    public const int DefaultMinutes = 30;
}
