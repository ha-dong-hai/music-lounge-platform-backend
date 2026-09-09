using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common;

/// <summary>
/// Người dùng còn làm được gì với buổi diễn này không.
/// </summary>
public enum ShowSaleState
{
    /// <summary>Mua được ngay bây giờ.</summary>
    OnSale,

    /// <summary>
    /// Chưa tới ngày mở bán. Chưa mua được, nhưng đây là thứ người hâm mộ muốn biết TRƯỚC để còn
    /// canh — cố ý tách khỏi <see cref="Closed"/> vì hai bên đáng được đối xử khác nhau.
    /// </summary>
    NotOpenYet,

    /// <summary>Hết vé, hoặc đã quá hạn bán. Người dùng không còn làm gì được nữa.</summary>
    Closed,

    /// <summary>
    /// Chưa cấu hình hạng vé nào, nên không có bằng chứng gì để kết luận. Cố ý KHÔNG coi là
    /// <see cref="Closed"/> — xem chú thích ở <see cref="ShowAvailability"/>.
    /// </summary>
    Unknown
}

/// <summary>
/// Buổi diễn này còn mua vé được không, và nếu không thì vì lý do gì.
///
/// <b>MLACP-327.</b> Danh sách duyệt có hẳn tham số <c>includeSoldOut</c> và lọc buổi hết vé. Danh
/// sách gợi ý thì không có gì tương đương, nên hệ thống chủ động đặt trước mặt người dùng những
/// buổi họ không thể mua vé. Gợi ý sinh ra để người dùng làm được gì đó với nó; mời mua một thứ
/// không mua được là lãng phí đúng thứ quý nhất — sự chú ý của họ.
///
/// <b>Ba lý do không mua được, và chúng không giống nhau.</b> Hết vé và hết hạn bán thì người dùng
/// không còn làm gì được nữa. Nhưng "chưa mở bán" thì ngược lại: đó chính là thứ người hâm mộ muốn
/// biết trước để còn canh. Gộp chung cả ba rồi đẩy xuống đáy là đẩy đúng thứ họ cần nhất đi.
///
/// <b>Thiếu thông tin không phải bằng chứng.</b> Buổi diễn vừa đăng thường chưa kịp cấu hình hạng
/// vé. Nếu coi "không có mức giá nào" là "không mua được" thì mọi buổi mới đều bị đẩy xuống đáy —
/// tức là xoá sạch phần cold start của buổi diễn vừa làm ở MLACP-321. Nên chỉ kết luận
/// <see cref="ShowSaleState.Closed"/> khi CÓ mức giá và mọi mức giá đều đóng.
///
/// Điều kiện mở bán lấy nguyên từ <see cref="TicketSaleWindow"/> — cùng quy tắc mà đường giữ chỗ và
/// đường bán vé tại quầy đang dùng, gồm cả trần giờ nhận khách cuối của BR-31. Một quy tắc, một chỗ.
/// </summary>
public static class ShowAvailability
{
    /// <param name="takenByPriceId">
    /// Số vé đã bán cộng số đang giữ chỗ, theo từng mức giá — lấy từ
    /// <c>ILoungeShowRepository.GetSoldAndHeldCountsByPriceAsync</c>. Cột <c>TicketPrice.Sold</c>
    /// KHÔNG dùng được: nó là cột cũ không bao giờ được ghi, luôn bằng 0.
    /// </param>
    public static ShowSaleState StateOf(
        LoungeShow show, IReadOnlyDictionary<int, int> takenByPriceId,
        DateTimeOffset now, int lastEntryMinutes)
    {
        var prices = show.TicketTiers
            .SelectMany(tier => tier.Prices)
            .Where(price => price.IsActive)
            .ToList();

        if (prices.Count == 0) return ShowSaleState.Unknown;

        var anyNotOpenYet = false;

        foreach (var price in prices)
        {
            var hasRoom = !price.Quota.HasValue
                || price.Quota.Value > takenByPriceId.GetValueOrDefault(price.Id);

            if (!hasRoom) continue;

            if (TicketSaleWindow.IsOpen(price, show, now, lastEntryMinutes))
                return ShowSaleState.OnSale;

            if (now < price.SaleStart) anyNotOpenYet = true;
        }

        return anyNotOpenYet ? ShowSaleState.NotOpenYet : ShowSaleState.Closed;
    }

    /// <summary>
    /// Có nên đẩy buổi diễn này xuống cuối danh sách gợi ý không. Chỉ đúng với
    /// <see cref="ShowSaleState.Closed"/> — xem chú thích lớp về lý do ba trạng thái kia thì không.
    /// </summary>
    public static bool ShouldDemote(ShowSaleState state) => state == ShowSaleState.Closed;
}
