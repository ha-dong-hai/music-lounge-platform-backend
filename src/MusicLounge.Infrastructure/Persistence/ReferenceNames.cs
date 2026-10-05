using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Infrastructure.Persistence;

/// <summary>
/// MLACP-672. Đổi một cặp (loại, mã) thành TÊN người đọc hiểu được — "Buổi diễn Retro Night", "Vé · Đêm Trịnh".
///
/// <para>Trước đây khiếu nại và hàng duyệt chỉ trả loại + mã của đối tượng, nên web in "Buổi diễn #01A10C82" (chủ dự án
/// phản hồi 05/10: "nhiều màn hình lấy id ra làm gì mà đáng lẽ phải là thông tin liên quan"). Tra ở MỘT chỗ để mọi DTO
/// cùng cách gọi tên, và mỗi loại chỉ tốn một truy vấn cho cả trang (không truy vấn theo từng dòng).</para>
///
/// <para>Loại không nhận ra hoặc đối tượng đã bị xoá thì không có tên (null) — web tự ghi "(không còn tồn tại)", không bịa.</para>
/// </summary>
internal static class ReferenceNames
{
    /// <param name="refs">Loại so khớp KHÔNG phân biệt hoa thường: show, livestream, ticket, tickettier, venue/lounge,
    /// galleryimage, tourscene, donation, penalty.</param>
    public static async Task<Dictionary<(string Type, Guid Id), string>> ResolveAsync(
        ApplicationDbContext db, IEnumerable<(string Type, Guid Id)> refs, CancellationToken ct)
    {
        var groups = refs.GroupBy(r => r.Type.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Select(r => r.Id).Distinct().ToList());
        var names = new Dictionary<(string, Guid), string>();
        void Put(string type, Guid id, string? name) { if (!string.IsNullOrWhiteSpace(name)) names[(type, id)] = name; }
        List<Guid> Ids(string type) => groups.TryGetValue(type, out var ids) ? ids : [];

        if (Ids("show") is { Count: > 0 } shows)
            foreach (var s in await db.Set<LoungeShow>().AsNoTracking().Where(x => shows.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct))
                Put("show", s.Id, s.Name);

        if (Ids("livestream") is { Count: > 0 } streams)
            foreach (var s in await db.Set<Livestream>().AsNoTracking().Where(x => streams.Contains(x.Id)).Select(x => new { x.Id, x.LoungeShow.Name }).ToListAsync(ct))
                Put("livestream", s.Id, $"Phát trực tuyến · {s.Name}");

        if (Ids("ticket") is { Count: > 0 } tickets)
            foreach (var t in await db.Set<Ticket>().AsNoTracking().Where(x => tickets.Contains(x.Id)).Select(x => new { x.Id, Tier = x.Tier.Name, Show = x.Show.Name }).ToListAsync(ct))
                Put("ticket", t.Id, $"Vé {t.Tier} · {t.Show}");

        if (Ids("tickettier") is { Count: > 0 } tiers)
            foreach (var t in await db.Set<TicketTier>().AsNoTracking().Where(x => tiers.Contains(x.Id)).Select(x => new { x.Id, x.Name, Show = x.LoungeShow.Name }).ToListAsync(ct))
                Put("tickettier", t.Id, $"Hạng vé {t.Name} · {t.Show}");

        foreach (var key in new[] { "venue", "lounge" })
            if (Ids(key) is { Count: > 0 } lounges)
                foreach (var l in await db.Set<MusicLoungeEntity>().AsNoTracking().Where(x => lounges.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct))
                    Put(key, l.Id, l.Name);

        if (Ids("galleryimage") is { Count: > 0 } images)
            foreach (var i in await db.Set<LoungeGalleryImage>().AsNoTracking().Where(x => images.Contains(x.Id)).Select(x => new { x.Id, x.Lounge.Name }).ToListAsync(ct))
                Put("galleryimage", i.Id, $"Ảnh của {i.Name}");

        if (Ids("tourscene") is { Count: > 0 } scenes)
            foreach (var s in await db.Set<VenueTourScene>().AsNoTracking().Where(x => scenes.Contains(x.Id)).Select(x => new { x.Id, x.Name, Lounge = x.Lounge.Name }).ToListAsync(ct))
                Put("tourscene", s.Id, $"Cảnh 360° {s.Name ?? ""} · {s.Lounge}".Replace("  ", " "));

        if (Ids("donation") is { Count: > 0 } donations)
            foreach (var d in await db.Set<Donation>().AsNoTracking().Where(x => donations.Contains(x.Id))
                         .Select(x => new { x.Id, Performer = x.Performance.Performer.Name, Show = x.Performance.LoungeShow.Name }).ToListAsync(ct))
                Put("donation", d.Id, $"Ủng hộ {d.Performer} · {d.Show}");

        if (Ids("penalty") is { Count: > 0 } penalties)
            foreach (var p in await db.Set<VenuePenalty>().AsNoTracking().Where(x => penalties.Contains(x.Id)).Select(x => new { x.Id, x.Lounge.Name }).ToListAsync(ct))
                Put("penalty", p.Id, $"Án phạt · {p.Name}");

        return names;
    }
}
