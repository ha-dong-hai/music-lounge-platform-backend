using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common;

/// <summary>
/// BR-31. Đợt bán vé đóng lúc nào.
///
/// <c>TicketPrice.SaleEnd</c> trước đây bắt buộc, nên mọi đợt bán phải chốt sẵn một mốc đóng cứng
/// từ lúc tạo buổi diễn. Với quầy vé thì đó là ràng buộc đặt sai chỗ: khán giả đến muộn vẫn vào
/// được, còn nhân viên ở quầy chỉ thấy hệ thống từ chối bán mà không biết vì sao.
///
/// Nhưng "bỏ trống thì bán tới hết buổi diễn" là câu trả lời sai theo hướng ngược lại. Bán vé khi
/// buổi diễn còn 5 phút là bán một thứ không còn gì để xem — khách trả nguyên giá cho phần chương
/// trình đã tan. Cái đúng là <b>giờ nhận khách cuối</b>: buổi diễn phải còn lại đủ một khoảng đáng
/// đồng tiền thì mới được bán.
///
/// Khoảng đó lấy theo mặc định của Eventbrite cho vé vào cửa tự do — nguyên văn tài liệu của họ:
/// "By default, your ticket sales end an hour before your event ends." Đây là nền tảng bán vé phổ
/// thông lớn nhất, và mốc của họ tính lùi từ lúc KẾT THÚC chứ không phải lúc bắt đầu, đúng với
/// tình huống một phòng trà bán vé cho người đến muộn.
///
/// Con số này khớp với thực tế ngành: khách mua vé khi chương trình đã diễn được một phần là
/// chuyện bình thường — các câu lạc bộ jazz bán riêng suất hai (Blue Note mở cửa 22:00 cho suất
/// 22:30; Chris' Jazz Cafe thu phí vào cửa theo từng suất) — nhưng luôn là bán một phần chương
/// trình có ranh giới rõ ràng, chứ không phải phần thừa cuối buổi.
///
/// Ngưỡng này là TRẦN CỨNG, không phải giá trị mặc định: nó áp cả khi Owner tự đặt mốc đóng. Nếu
/// chỉ là mặc định thì Owner vẫn đặt được mốc đóng đúng lúc buổi diễn kết thúc, và điều khoản bảo
/// vệ khách hàng này trở thành thứ tắt được bằng một ô nhập liệu. Owner đóng bán SỚM hơn thì được;
/// muộn hơn thì không.
///
/// Muốn bán cho khách đến muộn thì cách đúng là tạo một đợt bán riêng — có SaleStart, mức giá và
/// kênh bán của nó — đúng như các câu lạc bộ jazz bán suất hai với mức phí riêng, chứ không phải
/// bán vé nguyên giá cho nửa chương trình còn lại.
/// </summary>
public static class TicketSaleWindow
{
    /// <summary>
    /// Mặc định 60 phút, theo mốc Eventbrite dẫn ở trên. Đọc từ system_config
    /// (<c>ticket_last_entry_minutes</c>) nên nền tảng chỉnh được mà không phải sửa code.
    /// </summary>
    public const int DefaultLastEntryMinutes = 60;

    /// <summary>Mốc muộn nhất mà một vé còn được bán cho buổi diễn này.</summary>
    public static DateTimeOffset LastEntry(LoungeShow show, int lastEntryMinutes)
        => ShowSchedule.EffectiveEnd(show).AddMinutes(-lastEntryMinutes);

    /// <summary>
    /// Mốc đóng thực tế của đợt bán: mốc Owner đặt, nhưng không bao giờ muộn hơn giờ nhận khách cuối.
    /// </summary>
    public static DateTimeOffset EffectiveEnd(TicketPrice price, LoungeShow show, int lastEntryMinutes)
    {
        var lastEntry = LastEntry(show, lastEntryMinutes);
        return price.SaleEnd is { } explicitEnd && explicitEnd < lastEntry ? explicitEnd : lastEntry;
    }

    public static bool IsOpen(TicketPrice price, LoungeShow show, DateTimeOffset now, int lastEntryMinutes)
        => now >= price.SaleStart && now <= EffectiveEnd(price, show, lastEntryMinutes);
}
