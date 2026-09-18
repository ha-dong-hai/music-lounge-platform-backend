namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-458. Các mốc của hàng đợi poster, gom về MỘT nguồn.
///
/// Bốn giá trị dưới đây KHÔNG seed vào <c>system_config</c>, nên theo quy ước của dự án: <b>giá trị mặc định trong code
/// chính là chính sách đang chạy</b>. Đọc ở nhiều nơi (handler nhận đơn, job dọn đơn treo, test) nên phải cùng một hằng
/// có tên — đúng mẫu <c>RefundSla</c>, <c>ContentReportSla</c>, <c>ShowAutoEndGrace</c> đã làm.
///
/// Vì sao không đưa vào <c>system_config</c>: đây là tham số vận hành của một máy trạm cụ thể (máy chủ dự án chạy Google
/// Flow), không phải tham số nghiệp vụ mà người vận hành cần chỉnh qua giao diện Admin. Khi nào có nhiều máy trạm với tốc
/// độ khác nhau thì mới đáng chuyển sang cấu hình động.
/// </summary>
public static class PosterQueue
{
    /// <summary>
    /// Máy trạm giữ một đơn trong bao lâu trước khi hệ thống coi như nó đã chết.
    ///
    /// Một lượt hợp lệ chậm nhất đo được ngày 19/09 là 49 giây sinh ảnh + 39 giây xuất bản 2K ≈ 1,5 phút. Để 10 phút là
    /// rộng gấp nhiều lần, vì cái giá của hai phía không cân nhau: đặt quá ngắn thì đơn đang chạy tốt bị giao cho máy khác
    /// và tiêu hai lượt hạn mức Google cho một tấm poster; đặt quá dài thì người dùng chỉ phải chờ thêm vài phút.
    /// </summary>
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Đơn nằm chờ quá lâu vì không có máy trạm nào trực thì chuyển <c>Expired</c> và báo chủ phòng trà.
    ///
    /// 6 tiếng: máy trạm chỉ bật khi chủ dự án ngồi làm việc, nên đơn đặt buổi tối phải sống được qua đêm tới sáng hôm sau.
    /// Ngắn hơn thì gần như mọi đơn ngoài giờ hành chính đều chết oan.
    /// </summary>
    public static readonly TimeSpan QueueTimeout = TimeSpan.FromHours(6);

    /// <summary>
    /// Giao cho máy trạm tối đa bao nhiêu lần trước khi bỏ cuộc. Ba lần đủ để vượt qua một lần deploy hoặc một lần mất
    /// mạng; nhiều hơn nữa thì lỗi gần như chắc chắn nằm ở chính lời nhắc, thử lại chỉ tốn hạn mức Google.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Khổ ảnh yêu cầu máy trạm sinh. Poster buổi hòa nhạc là ảnh dọc; 3:4 là khổ dọc gần nhất mà Google Flow hỗ trợ
    /// (các khổ còn lại: 1:1, 9:16, 16:9, 4:3). 9:16 quá cao cho poster, hợp với story mạng xã hội hơn.
    /// </summary>
    public const string AspectRatio = "3:4";

    /// <summary>Câu báo cho chủ phòng trà khi máy trạm nhận đơn rồi chết giữa chừng quá số lần cho phép.</summary>
    public const string ThongBaoMayTramKhongPhanHoi =
        "Máy tạo ảnh không phản hồi nên poster chưa tạo được. Bạn không bị trừ lượt poster nào, vui lòng thử lại sau.";

    /// <summary>Câu báo khi đơn nằm chờ hết thời hạn mà không có máy trạm nào đến nhận.</summary>
    public const string ThongBaoHetHanCho =
        "Poster của bạn chưa tạo được vì hệ thống tạo ảnh đang tạm nghỉ. Bạn không bị trừ lượt nào — " +
        "vui lòng thử lại, hoặc tự tải poster của bạn lên.";
}
