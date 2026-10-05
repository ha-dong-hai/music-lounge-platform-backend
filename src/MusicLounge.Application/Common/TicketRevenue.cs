using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-616. Vé ở trạng thái nào thì là DOANH THU — định nghĩa duy nhất cho mọi báo cáo tiền.
///
/// <b>Lỗi đã xảy ra:</b> trang thống kê và báo cáo doanh thu của chủ phòng trà (cả bản xuất CSV nộp kế toán), cùng số vé
/// đã bán của nền tảng, chỉ đếm <see cref="TicketStatus.Confirmed"/>. Soát vé vào cửa chuyển vé sang
/// <see cref="TicketStatus.Used"/>, nên mỗi lượt soát làm doanh thu tụt đi đúng giá vé đó. Đo trên dữ liệu mẫu
/// 04/10/2026: trang thống kê chỉ hiện khoảng 1/3 doanh thu vé thật. Cùng lớp lỗi với <see cref="TicketOccupancy"/>
/// (MLACP-459), nhưng là một câu hỏi khác nên là một định nghĩa khác.
///
/// <b>Căn cứ:</b> doanh thu ghi nhận theo giao dịch đã thu tiền (VAS 14 — Doanh thu và thu nhập khác), không theo việc
/// khách đã vào cửa hay chưa.
///
/// <b>Khác <see cref="TicketOccupancy.ChiemCho"/> ở hai chỗ:</b>
/// <see cref="TicketStatus.Pending"/> giữ chỗ nhưng CHƯA thu tiền; <see cref="TicketStatus.Refunded"/> vẫn chiếm chỗ
/// (người đó đã vào xem) nhưng tiền đã trả lại cho khách — cả hai đều không phải doanh thu.
///
/// <b>Trần giới hạn:</b> báo cáo tính theo từng vé nên không thấy được phần phí huỷ phòng trà được giữ lại khi chỉ hoàn
/// một phần (vé <see cref="TicketStatus.Cancelled"/> với tỉ lệ hoàn dưới 100%). Đường nâng cấp: tính doanh thu từ thanh
/// toán trừ các khoản hoàn đã duyệt (sổ cái), thay vì từ trạng thái vé.
/// </summary>
public static class TicketRevenue
{
    /// <summary>Vé đã thu tiền và chưa hoàn. Dạng mảng để EF Core dịch thành <c>IN (...)</c>.</summary>
    public static readonly TicketStatus[] DaThuTien =
    [
        TicketStatus.Confirmed,
        TicketStatus.Used
    ];
}
