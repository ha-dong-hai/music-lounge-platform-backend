using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets;

/// <summary>
/// MLACP-499. Bộ lọc "Vé của tôi". Mọi trường đều tuỳ chọn — null là không lọc theo trường đó.
/// <para><see cref="When"/>: Upcoming = buổi bắt đầu sau giờ hiện tại, Past = buổi đã bắt đầu. Cùng quy tắc trang Vé của
/// tôi đang dùng (so ScheduledStart), nên buổi ĐANG diễn rơi vào Past. Đổi sang so giờ kết thúc là đổi nghĩa tab trên
/// giao diện — để quyết riêng, không gộp vào thay đổi này.</para>
/// <para><see cref="Keyword"/>: khớp không phân biệt hoa thường trên tên buổi diễn, tên phòng trà, tên hạng vé và mã vé
/// — đúng bốn thứ ô tìm kiếm của trang đang tìm, để chuyển lọc về máy chủ không làm mất kết quả nào người dùng quen thấy.</para>
/// </summary>
public sealed record MyTicketFilter(
    TicketStatus? Status = null,
    AccessType? AccessType = null,
    TicketTimeFilter? When = null,
    string? Keyword = null);
