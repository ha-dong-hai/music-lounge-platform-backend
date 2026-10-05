using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

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

    /// <summary>Một biến động tiền: dương ở ngày bán, âm ở ngày duyệt hoàn.</summary>
    public sealed record BienDong(DateTimeOffset Luc, Payment ThanhToan, decimal Gmv, decimal ThucNhan);

    /// <summary>
    /// MLACP-616: mọi biến động doanh thu theo thời gian — một định nghĩa cho MỌI màn hình quản trị (bảng điều khiển,
    /// thẻ tổng quan), để hai con số cùng tên không bao giờ lệch nhau.
    ///
    /// <para>Doanh số ghi ở NGÀY BÁN, gồm cả thanh toán về sau bị hoàn toàn bộ (Refunded); mỗi khoản hoàn đã duyệt ghi
    /// số ÂM ở NGÀY DUYỆT HOÀN. Căn cứ VAS 14 — hàng bán bị trả lại ghi giảm trừ doanh thu ở kỳ phát sinh, không sửa kỳ đã
    /// bán. Trước đây các màn hình chỉ lấy thanh toán Confirmed: hoàn một phần thì vẫn tính đủ phí, hoàn toàn bộ thì
    /// thanh toán biến khỏi tháng bán (số liệu tháng cũ tự đổi).</para>
    ///
    /// <para>Phần phí của khoản hoàn = <see cref="CuaThanhToan"/> × (tiền hoàn / tiền gốc), làm tròn về đồng — để hiển
    /// thị; số đảo chính xác tới từng đồng nằm ở sổ cái (ProcessRefundRequest phân bổ lũy kế).</para>
    ///
    /// <para>Trần giới hạn: nạp toàn bộ thanh toán đã bán và khoản hoàn đã duyệt rồi lọc ở ứng dụng (giới hạn SQLite của
    /// bộ test với so sánh DateTimeOffset). Đường nâng cấp: bảng tổng hợp theo ngày do job định kỳ ghi.</para>
    /// </summary>
    public static async Task<IReadOnlyList<BienDong>> BienDongAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var thanhToan = (await uow.Repository<Payment, Guid>()
                .FindAsync(p => p.Status == PaymentStatus.Confirmed || p.Status == PaymentStatus.Refunded, ct))
            .Where(p => p.PaidAt.HasValue)
            .ToDictionary(p => p.Id);

        var maThanhToan = thanhToan.Keys.ToList();
        var hoan = (await uow.Repository<RefundRequest, Guid>().FindAsync(
                r => r.Status == RefundRequestStatus.Approved && maThanhToan.Contains(r.PaymentId), ct))
            .Where(r => r.ResolvedAt.HasValue && r.AmountApproved is > 0m);

        var ketQua = thanhToan.Values
            .Select(p => new BienDong(p.PaidAt!.Value, p, p.GrossAmount, CuaThanhToan(p)))
            .ToList();
        foreach (var r in hoan)
        {
            var p = thanhToan[r.PaymentId];
            var soHoan = r.AmountApproved!.Value;
            var phiHoan = p.GrossAmount > 0m
                ? Math.Round(CuaThanhToan(p) * soHoan / p.GrossAmount, 0, MidpointRounding.AwayFromZero)
                : 0m;
            ketQua.Add(new BienDong(r.ResolvedAt!.Value, p, -soHoan, -phiHoan));
        }
        return ketQua;
    }
}
