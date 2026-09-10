namespace MusicLounge.Domain.Enums;

public enum TicketStatus
{
    Pending,
    Confirmed,
    Used,
    Cancelled,

    /// <summary>
    /// MLACP-347: vé <b>đã dùng</b> (đã vào xem) rồi mới được hoàn — hiện chỉ do buổi phát sóng bị
    /// cắt ngang dưới ngưỡng thời lượng. Khác <see cref="Cancelled"/> (chưa từng dùng) ở chỗ người
    /// giữ vé vẫn còn quyền đánh giá: họ đã chứng kiến buổi diễn hỏng, và tước quyền đó thì phòng
    /// trà thoát được đánh giá xấu của đúng buổi hỏng ấy.
    /// </summary>
    Refunded
}
