namespace MusicLounge.Domain.Entities;

/// <summary>
/// Người dùng không muốn hệ gợi ý nhắc tới phòng trà này nữa.
///
/// Đối xứng với <see cref="Follow"/>: cùng hình dạng, ngược dấu. Đây là dữ liệu người dùng TỰ KHAI
/// bằng một hành động chủ động, không phải thứ hệ thống suy ra — nên nó không nằm sau sự đồng ý cho
/// phân tích hành vi, đúng như sở thích khai ở bước onboarding.
///
/// <b>Tắt tiếng không phải kiểm duyệt.</b> Nó chỉ chặn phòng trà đó khỏi những gì hệ thống CHỦ ĐỘNG
/// đẩy tới người dùng. Người dùng vẫn tìm ra được nếu họ tự đi tìm — cắt cả đường tìm kiếm là quyết
/// hộ họ một chuyện họ chưa hề yêu cầu.
/// </summary>
public sealed class LoungeMute : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int LoungeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public MusicLounge Lounge { get; set; } = null!;
}
