using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.FnbOrders;

/// <summary>Khu mà khán giả đang ngồi, suy từ vé của chính họ — xem <see cref="GuestSeat"/>.</summary>
public sealed record GuestSeatDto(Guid ShowId, string ShowName, Guid ZoneId, string ZoneName);

/// <summary>
/// MLACP-630 — "khách đang ngồi ở khu nào" khi gọi món.
///
/// Chủ dự án 04/10/2026: khán giả đặt món bằng điện thoại thì hệ thống phải có cơ chế ghi nhận vị trí của khách, vì lúc
/// mua vé họ chỉ chọn KHU (hệ thống bán theo khu, không bán từng ghế). Trước task này đơn gọi món chỉ mang một dòng chữ
/// tự gõ ("Bàn 12"); <c>FnbOrder.ZoneId</c> có sẵn trong lược đồ nhưng không client nào gửi và không DTO nào trả ra —
/// đúng kiểu cột "ghi mà không ai đọc". Nhân viên nhận đơn "Bàn góc" mà không biết góc của khu nào.
///
/// Khu KHÔNG do khách tự khai: lấy từ vé vào cửa của chính họ cho buổi đang diễn (hoặc sắp mở cửa) ở phòng trà đó. Một
/// nguồn cho cả hai nơi cần câu trả lời này — <c>GET /fnb-orders/my-seat</c> (app hiện cho khách xem trước) và
/// <c>CreateFnbOrderCommandHandler</c> (ghi vào đơn) — để thứ khách thấy và thứ được ghi không bao giờ lệch nhau.
///
/// GIỚI HẠN CỐ Ý: khu là một vùng nhiều chỗ (hàng chục ghế), chưa đủ để mang món ra tận nơi — đơn vẫn cần dòng chữ vị
/// trí trong khu (số bàn / mô tả). Đường nâng cấp khi phòng trà đánh số bàn: thêm bảng bàn theo khu rồi cho chọn bàn.
/// </summary>
internal static class GuestSeat
{
    /// <summary>
    /// Vé của một buổi CHƯA bắt đầu chỉ được tính từ chừng này giờ trước giờ diễn — khoảng khách đã vào phòng trà ngồi
    /// chờ. Không có cấu hình "giờ mở cửa" trong hệ thống nên đây là hằng số; phòng trà mở cửa sớm hơn thì khách gọi món
    /// vẫn được, chỉ là đơn chưa tự mang khu (nhân viên đọc dòng vị trí khách ghi).
    /// </summary>
    public const int DoorsOpenHoursBeforeStart = 3;

    private static readonly TicketStatus[] Held = [TicketStatus.Confirmed, TicketStatus.Used];

    public static async Task<GuestSeatDto?> ResolveAsync(
        IUnitOfWork uow, Guid userId, Guid loungeId, DateTimeOffset now, CancellationToken ct)
    {
        // Chỉ vé CÒN HIỆU LỰC (đã trả tiền, hoặc đã soát) và là vé vào cửa có khu. Vé xem trực tuyến không có khu.
        var tickets = await uow.Repository<Ticket, Guid>().FindAsync(
            t => t.BuyerId == userId && Held.Contains(t.Status)
                 && t.Show.LoungeId == loungeId && t.Tier.ZoneId != null, ct);
        if (tickets.Count == 0) return null;

        var showIds = tickets.Select(t => t.ShowId).Distinct().ToList();
        var shows = (await uow.Repository<LoungeShow, Guid>().FindAsync(s => showIds.Contains(s.Id), ct))
            .Where(s => IsHappeningNow(s, now))
            .ToDictionary(s => s.Id);
        if (shows.Count == 0) return null;

        // Nhiều vé cùng lúc (mua hai hạng, hoặc hai buổi nối nhau): vé ĐÃ SOÁT là bằng chứng mạnh nhất rằng khách đang
        // ngồi ở khu đó; rồi tới buổi đang diễn; rồi buổi sắp diễn gần nhất.
        var chosen = tickets
            .Where(t => shows.ContainsKey(t.ShowId))
            .OrderByDescending(t => t.Status == TicketStatus.Used)
            .ThenByDescending(t => shows[t.ShowId].Status == LoungeShowStatus.Ongoing)
            .ThenBy(t => shows[t.ShowId].ScheduledStart)
            .First();

        var tier = await uow.Repository<TicketTier, Guid>().GetByIdAsync(chosen.TierId, ct);
        if (tier?.ZoneId is not { } zoneId) return null;
        var zone = await uow.Repository<SeatingZone, Guid>().GetByIdAsync(zoneId, ct);
        if (zone is null) return null;

        var show = shows[chosen.ShowId];
        return new GuestSeatDto(show.Id, show.Name, zone.Id, zone.Name);
    }

    private static bool IsHappeningNow(LoungeShow show, DateTimeOffset now)
        => show.Status == LoungeShowStatus.Ongoing
           || (show.Status == LoungeShowStatus.Published
               && now >= show.ScheduledStart.AddHours(-DoorsOpenHoursBeforeStart)
               && now <= ShowSchedule.EffectiveEnd(show));
}
