using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.TicketTiers;

/// <summary>
/// MLACP-589 (chủ dự án 04/10/2026): trong MỘT buổi diễn, mỗi hạng vé vào cửa gắn với ĐÚNG MỘT khu ghế và mỗi khu chỉ
/// thuộc MỘT hạng vé — "1 hạng vé trong buổi diễn sẽ được set với 1 khu vực trong sơ đồ". Nhờ vậy khán giả chạm một khu
/// trên sơ đồ 2D/3D là chọn luôn hạng vé, không còn bước chọn hạng vé riêng rồi mới dò khu.
///
/// Một nguồn cho cả hai đường ghi ZoneId (tạo hạng vé và gắn khu sau) để hai nơi không lệch luật nhau.
///
/// Giới hạn cố ý: hạng vé vào cửa CŨ chưa gắn khu (tạo trước luật này) vẫn giữ nguyên và vẫn bán được — luật chỉ chặn
/// lúc TẠO MỚI. Đường nâng cấp khi cần chặt hơn: chặn đăng buổi diễn (Publish) khi còn hạng vé vào cửa chưa có khu.
/// </summary>
internal static class TierZoneRules
{
    /// <summary>Khu phải thuộc đúng phòng trà của buổi diễn, đang hoạt động, và chưa hạng vé nào KHÁC của buổi này dùng.</summary>
    public static async Task<SeatingZone> EnsureZoneIsFreeForShowAsync(
        IUnitOfWork uow, LoungeShow show, Guid zoneId, Guid? exceptTierId, CancellationToken ct)
    {
        var zone = await uow.Repository<SeatingZone, Guid>().GetByIdAsync(zoneId, ct);
        if (zone is null || zone.LoungeId != show.LoungeId)
            throw new DomainException("Khu ghế không thuộc phòng trà của buổi diễn này.");
        if (!zone.IsActive)
            throw new DomainException("Khu ghế này đang tạm ngưng.");

        var others = await uow.Repository<TicketTier, Guid>().FindAsync(
            t => t.LoungeShowId == show.Id && t.ZoneId == zoneId, ct);
        var taken = others.FirstOrDefault(t => t.Id != exceptTierId);
        if (taken is not null)
            throw new DomainException(
                $"Khu \"{zone.Name}\" đã thuộc hạng vé \"{taken.Name}\" của buổi diễn này. Mỗi khu chỉ gắn với một hạng vé — " +
                "hãy chọn khu khác, hoặc thêm một mức giá vào hạng vé đó.");
        return zone;
    }
}
