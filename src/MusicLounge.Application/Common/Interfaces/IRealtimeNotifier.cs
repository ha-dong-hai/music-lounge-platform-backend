namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// MLACP-669. Báo cho trình duyệt đang mở biết "dữ liệu của bạn vừa đổi" — để màn hình tải lại ngay thay vì chờ người dùng
/// bấm F5. Trước task này web không có kênh nào như vậy: Admin từ chối tài khoản nhận tiền thì màn của chủ phòng trà vẫn
/// hiện "Chờ Admin duyệt" cho tới khi tải lại trang, và chuông thông báo chỉ đếm một lần lúc mở trang.
///
/// <para>Sự kiện chỉ mang CHỦ ĐỀ + mã tham chiếu, không mang dữ liệu: trình duyệt nhận xong tự gọi lại API như bình
/// thường, nên mọi kiểm quyền vẫn nằm ở API — kênh này không thể làm lộ thứ mà API không cho đọc.</para>
///
/// <para>Phát SAU KHI dữ liệu đã commit (xem <c>RealtimeOutbox</c>): phát trước thì trình duyệt tải lại đúng lúc
/// transaction chưa xong và đọc phải dữ liệu cũ — tức là vẫn "không cập nhật", chỉ là khó thấy hơn.</para>
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Không bao giờ ném lỗi: kênh thời gian thực là tiện ích, hỏng nó không được làm hỏng nghiệp vụ đã lưu.</summary>
    Task PublishAsync(IReadOnlyCollection<RealtimeEvent> events, CancellationToken ct = default);
}

/// <param name="UserId">Người nhận; <c>null</c> = gửi cho mọi Admin đang mở trang (hàng việc chờ dùng chung).</param>
/// <param name="Topic">
/// <c>"notification"</c> khi người nhận có thông báo mới; còn lại là khoá hàng việc chờ của Admin
/// (<c>GetAdminWorkQueueQueryHandler</c>: "shows", "venues", "kyc-reviews", "bank-accounts"…) — cùng một khoá để web dùng
/// một bảng ánh xạ cho cả số đếm trên menu lẫn danh sách.
/// </param>
public sealed record RealtimeEvent(Guid? UserId, string Topic, string? ReferenceType = null, string? ReferenceId = null);
