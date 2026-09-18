using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Tickets;

/// <summary>
/// MLACP-446. Vé bán tại quầy (tiền mặt) có đi qua sổ cái và có được lên lịch chi trả như vé online
/// hay không. Một nguồn duy nhất cho hai nơi phải đồng ý với nhau tuyệt đối:
/// <c>WriteTicketLedgerHandler</c> (ghi sổ) và <c>ScheduleSettlementHandler</c> (lên lịch chi tiền).
///
/// <para>Trước task này mỗi nơi tự ghi <c>false</c>, kèm chú thích dặn "keep both in sync" — đúng kiểu
/// lời hứa không có cơ chế mà dự án này đã gặp nhiều lần. Và đây là trường hợp lệch nhau tốn tiền
/// thật: nếu chỉ <c>ScheduleSettlementHandler</c> bật mà sổ cái không ghi, nền tảng sẽ chuyển khoản
/// cho phòng trà một khoản tiền mà họ ĐÃ thu tiền mặt tại quầy — trả hai lần cho cùng một vé. Lệch
/// theo chiều ngược lại thì sổ cái có bút toán mà không bao giờ có lệnh chi tương ứng.</para>
///
/// <para>Mặc định TẮT (quyết định sản phẩm 09/08/2026): tiền vé tại quầy do phòng trà thu trực tiếp,
/// không đi qua tài khoản cổng thanh toán của nền tảng, nên nền tảng không có gì để chia lại — doanh
/// thu từ vé tại quầy đến từ phí gói dịch vụ. Chỉ bật khi tiền bán tại quầy thật sự chảy về nền tảng
/// (ví dụ máy POS do nền tảng vận hành đặt tại phòng trà). <c>walkin_commission_enabled</c> KHÔNG được
/// seed nên giá trị mặc định ở đây chính là trạng thái đang chạy.</para>
/// </summary>
public static class WalkInCommission
{
    public const bool DefaultEnabled = false;

    public static Task<bool> IsEnabledAsync(ISystemConfigService config, CancellationToken ct)
        => config.GetBoolAsync(ConfigKeys.WalkInCommissionEnabled, DefaultEnabled, ct);
}
