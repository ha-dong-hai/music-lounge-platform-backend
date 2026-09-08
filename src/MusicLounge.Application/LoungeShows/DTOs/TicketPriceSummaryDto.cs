using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record TicketPriceSummaryDto(
    int Id,
    string Name,
    decimal Price,
    int? Quota,
    DateTimeOffset SaleStart,
    /// <summary>
    /// Mốc đóng bán thực tế: mốc Owner đặt, hoặc giờ nhận khách cuối nếu Owner không đặt — và
    /// không bao giờ muộn hơn giờ nhận khách cuối trong cả hai trường hợp (BR-31). Luôn là một mốc
    /// có thật, nên trường này giữ nguyên kiểu không-null như trước.
    /// </summary>
    DateTimeOffset SaleEnd,
    PurchaseChannel PurchaseChannel,
    int? AvailableSlots,
    /// <summary>
    /// True khi mốc trong SaleEnd là do hệ thống suy ra — Owner không đặt mốc nào, hoặc mốc Owner
    /// đặt muộn hơn giờ nhận khách cuối nên bị chặn lại. Để màn hình sửa đợt bán hiển thị đúng
    /// tình trạng thay vì điền sẵn một mốc Owner chưa từng chọn.
    /// </summary>
    bool SaleEndIsAutomatic);
