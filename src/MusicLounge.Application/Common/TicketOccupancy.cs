using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-459. Vé ở trạng thái nào thì CHỖ NGỒI coi như đã có người — định nghĩa duy nhất cho cả nền tảng.
///
/// <b>Lỗi đã xảy ra:</b> phép đếm chỗ đã chiếm chỉ tính <see cref="TicketStatus.Confirmed"/> và
/// <see cref="TicketStatus.Pending"/>. Soát vé vào cửa chuyển vé sang <see cref="TicketStatus.Used"/>, nên mỗi lượt soát
/// làm con số "đã bán" tụt đi một, trong khi người đó đang ngồi trong phòng. Quầy vé lại được bán TRONG LÚC buổi diễn
/// đang chạy (BR-31: bán tới trước giờ kết thúc 60 phút), nên hai cửa sổ chồng nhau: soát 30 vé thì bán vượt được 30 vé.
/// Sáu chỗ trong mã nguồn cùng hỏi "còn bao nhiêu chỗ" mà mỗi chỗ tự viết lại danh sách trạng thái — sửa một chỗ thì
/// năm chỗ kia vẫn sai. Nên gom về đây.
///
/// <b>Vì sao <see cref="TicketStatus.Refunded"/> cũng tính là chiếm chỗ:</b> theo chú thích trong
/// <see cref="TicketStatus"/>, trạng thái này chỉ dành cho vé ĐÃ DÙNG rồi mới được hoàn (buổi phát bị cắt ngang dưới
/// ngưỡng thời lượng). Hai chỗ duy nhất đặt trạng thái này đều theo đúng mẫu
/// <c>Status == Used ? Refunded : Cancelled</c> (<c>RefundUndeliveredLivestreamTicketsJob</c> và
/// <c>ResolveComplaintCommandHandler</c>) — nghĩa là người giữ vé đã vào xem. Tiền trả lại không làm cái ghế trống ra.
///
/// <b>Vì sao <see cref="TicketStatus.Cancelled"/> KHÔNG tính:</b> đó là vé chưa từng được dùng — chỗ đó trống thật và
/// phải bán lại được, nếu không thì mỗi lần huỷ vé là mất vĩnh viễn một chỗ ngồi.
/// </summary>
public static class TicketOccupancy
{
    /// <summary>
    /// Bốn trạng thái chiếm chỗ. Dùng dạng mảng để EF Core dịch thành mệnh đề <c>IN (...)</c> ở phía cơ sở dữ liệu,
    /// thay vì phải nối chuỗi <c>||</c> ở từng nơi gọi rồi quên mất một giá trị.
    /// </summary>
    public static readonly TicketStatus[] ChiemCho =
    [
        TicketStatus.Confirmed,
        TicketStatus.Pending,
        TicketStatus.Used,
        TicketStatus.Refunded
    ];
}
