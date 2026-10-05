using System.Globalization;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-645 — mọi số tiền in vào chữ TIẾNG VIỆT gửi tới người dùng (thông báo, email, thông báo lỗi) đi qua đây.
///
/// <para>Trước đây các câu viết <c>{amount:N0}đ</c>: định dạng N0 lấy dấu ngăn cách hàng nghìn của culture MÁY CHỦ, nên
/// thông báo ra "783,000đ" (kiểu Mỹ) thay vì "783.000đ" (đo trên DB cục bộ 05/10/2026 — Windows en-US). Đặt culture mặc
/// định của cả tiến trình thành vi-VN thì sửa được chữ nhưng làm hỏng chỗ đọc số thập phân "0.05" của system_config — nên
/// định dạng tại đây, có chủ đích, giống cách <see cref="VietnamTime"/> làm với giờ.</para>
///
/// <para>VND không có đơn vị lẻ (CLAUDE.md "Tiền"): làm tròn về đồng, không in phần thập phân. Câu tiếng Anh giữ
/// <c>{amount:N0} VND</c> — dấu phẩy là đúng cho người đọc tiếng Anh.</para>
/// </summary>
public static class VietnamMoney
{
    private static readonly NumberFormatInfo So = new() { NumberGroupSeparator = ".", NumberDecimalSeparator = "," };

    /// <summary>"783.000đ".</summary>
    public static string Format(decimal amount)
        => Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("#,0", So) + "đ";
}
