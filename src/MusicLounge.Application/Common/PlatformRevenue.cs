using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-463. "Doanh thu nền tảng" là gì — một định nghĩa, dùng chung cho mọi màn hình.
///
/// <b>Lỗi đã có:</b> các màn hình quản trị đang cộng MỌI bút toán ghi CÓ vào tài khoản nền tảng rồi gọi đó là doanh thu.
/// Nhưng tài khoản nền tảng nhận hai loại tiền khác hẳn nhau:
/// <list type="bullet">
/// <item><b>Hoa hồng</b> — tiền nền tảng thật sự được hưởng.</item>
/// <item><b>Tiền giữ hộ</b> — phần của chủ phòng trà, nằm tạm ở tài khoản nền tảng cho tới khi quyết toán
/// (<c>WriteTicketLedgerHandler</c> ghi rõ "Giữ hộ chủ phòng trà … chờ settlement"; đơn gọi món thì TOÀN BỘ số tiền là
/// giữ hộ). Số này rồi sẽ đi ra khỏi nền tảng.</item>
/// </list>
/// Cộng cả hai lại thì con số "doanh thu" phồng lên gần bằng tổng tiền người mua trả — tức là nói với chủ dự án rằng
/// nền tảng ăn gần trọn mỗi vé. Đó là thứ không được phép sai trong một đồ án có luồng tiền thật.
///
/// <b>Không phân biệt bằng chữ trong <c>Description</c> của bút toán</b>: đó là câu tiếng Việt cho người đọc, đổi một
/// chữ là hỏng phép tính. Dùng cột tiền có sẵn trên chính khoản thanh toán.
/// </summary>
public static class PlatformRevenue
{
    /// <summary>
    /// Phần nền tảng thật sự hưởng từ một khoản thanh toán đã xác nhận.
    ///
    /// <list type="bullet">
    /// <item><b>Vé và donate</b>: <c>PlatformFee</c> — phần còn lại là của phòng trà/nghệ sĩ, nền tảng chỉ giữ hộ.
    /// Vé bán tại quầy mặc định không có hoa hồng nên bằng 0, đúng như chính sách đang chạy.</item>
    /// <item><b>Gói dịch vụ</b>: TOÀN BỘ số tiền — chủ phòng trà trả thẳng cho nền tảng, không có ai để giữ hộ
    /// (<c>ProcessSubscriptionPaymentCommandHandler</c> ghi CÓ toàn bộ vào tài khoản nền tảng).</item>
    /// <item><b>Đơn gọi món</b>: 0 — toàn bộ là giữ hộ, hiện chưa thu hoa hồng ở bước thanh toán.</item>
    /// </list>
    /// </summary>
    public static decimal CuaThanhToan(Payment thanhToan) => thanhToan.ReferenceType switch
    {
        "Subscription" => thanhToan.GrossAmount,
        "TicketHold" or "WalkIn" or "Donation" => thanhToan.PlatformFee,
        _ => 0m
    };
}
