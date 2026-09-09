namespace MusicLounge.Application.Analytics.Common;

/// <summary>
/// Công việc tính lại gợi ý nền có tính ra được gì cho người này không.
///
/// <b>MLACP-328.</b> Điều kiện này trước đây chỉ nằm BÊN TRONG chính công việc đó, dưới dạng một
/// nhánh thoát sớm. Bên đặt lịch không hỏi được, nên nó cứ đặt — và với người chưa có gì để tính
/// thì thành một vòng không có điểm dừng:
///
/// <code>
/// job thoát sớm, không ghi dòng nào  ->  cache vẫn rỗng
/// cache rỗng                         ->  request sau lại đặt lịch tiếp
/// </code>
///
/// Mỗi lần mở màn hình gợi ý là thêm một việc vào hàng đợi, mãi mãi. Và nó rơi đúng vào nhóm người
/// mới: đã bấm đồng ý cho phân tích hành vi nhưng chưa kịp khai sở thích — <b>người càng hợp tác
/// thì càng tạo rác</b>.
///
/// Đưa điều kiện ra đây để bên gọi hỏi được TRƯỚC khi đặt lịch, và để không có hai bản điều kiện
/// gần giống nhau ở hai nơi rồi lệch dần — đúng lớp lỗi MLACP-322 đã trả giá.
/// </summary>
public static class RecommendationRefresh
{
    /// <summary>
    /// Số dòng nhật ký hành vi tối thiểu để chạy nhánh hybrid (có lọc cộng tác). Dưới ngưỡng này
    /// thì chỉ còn nhánh theo nội dung, và nhánh đó cần sở thích tự khai hoặc phòng trà đang theo dõi.
    /// </summary>
    public const int MinBehaviourLogs = 5;

    /// <summary>
    /// Có ít nhất một nhánh chạy được không. Sai nghĩa là công việc nền sẽ thoát sớm mà không ghi
    /// dòng nào, nên đặt lịch chỉ tạo rác.
    /// </summary>
    public static bool CanProduceAnything(
        int behaviourLogCount, bool hasDeclaredTaste, bool followsAnyVenue)
        => behaviourLogCount >= MinBehaviourLogs || hasDeclaredTaste || followsAnyVenue;
}
