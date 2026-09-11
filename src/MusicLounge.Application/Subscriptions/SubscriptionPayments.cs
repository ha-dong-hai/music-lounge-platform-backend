namespace MusicLounge.Application.Subscriptions;

/// <summary>
/// MLACP-366 — thanh toán gói dịch vụ của chủ phòng trà.
///
/// <para>Khác vé, F&amp;B hay donate: gói là <b>doanh thu của chính nền tảng</b>. Không có phòng trà nào đứng
/// giữa, không có phí hay thuế khấu trừ hộ ai, không có phần nào phải giải ngân — bút toán gốc chỉ có
/// Gateway nợ / Platform có. Mọi chỗ xử lý tiền của một thanh toán loại này phải biết điều đó, thay vì
/// đi tìm một chủ phòng trà không tồn tại.</para>
/// </summary>
public static class SubscriptionPayments
{
    public const string ReferenceType = "Subscription";
}
