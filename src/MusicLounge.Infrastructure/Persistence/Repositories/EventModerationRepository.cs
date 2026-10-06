using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class EventModerationRepository
    : Repository<EventModeration, Guid>, IEventModerationRepository
{
    private readonly ApplicationDbContext _ctx;

    public EventModerationRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<PaginatedResult<EventModerationDto>> GetPendingAsync(
        ModerationTargetType? targetType, Guid? targetId, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = _ctx.EventModerations
            .AsNoTracking()
            .Where(m => m.AdminDecision == null);

        if (targetType.HasValue)
            baseQuery = baseQuery.Where(m => m.TargetType == targetType.Value);
        // MLACP-504: trang chi tiết buổi diễn của Admin tải 100 bản chờ rồi quét tìm bản của buổi đang mở — quá 100 bản
        // chờ thì hộp duyệt không mở được. Lọc thẳng theo đối tượng.
        if (targetId.HasValue)
            baseQuery = baseQuery.Where(m => m.TargetId == targetId.Value);

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(m => m.AiScore)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new EventModerationDto(
                m.Id,
                m.TargetType.ToString(),
                m.TargetId,
                m.AiScore,
                m.RiskLevel == null ? null : m.RiskLevel.ToString(),
                m.FlagReason,
                m.AiRecommendation == null ? null : m.AiRecommendation.ToString(),
                m.AdminId,
                m.AdminDecision == null ? null : m.AdminDecision.ToString(),
                m.ReviewNote,
                m.CreatedAt,
                m.SlaDeadline,
                m.ReviewedAt))
            .ToListAsync(ct);

        // MLACP-672: tên thứ đang chờ duyệt — trước đây hộp duyệt ghi "Buổi diễn #01A10C82".
        var names = await ReferenceNames.ResolveAsync(_ctx, items.Select(i => (i.TargetType, i.TargetId)), ct);
        items = items.Select(i => i with { TargetName = names.GetValueOrDefault((i.TargetType.ToLowerInvariant(), i.TargetId)) }).ToList();

        // MLACP-692: ảnh + tên phòng trà cho ảnh thư viện / cảnh 360 — Admin duyệt ảnh mà không thấy ảnh thì không duyệt được.
        var idAnh = items.Where(i => i.TargetType == nameof(ModerationTargetType.GalleryImage)).Select(i => i.TargetId).ToList();
        var idCanh = items.Where(i => i.TargetType == nameof(ModerationTargetType.TourScene)).Select(i => i.TargetId).ToList();
        var anh = idAnh.Count == 0 ? [] : await _ctx.Set<LoungeGalleryImage>().AsNoTracking().Where(g => idAnh.Contains(g.Id))
            .Select(g => new { g.Id, g.ImageUrl, Ten = g.Lounge.Name }).ToListAsync(ct);
        var canh = idCanh.Count == 0 ? [] : await _ctx.Set<VenueTourScene>().AsNoTracking().Where(s => idCanh.Contains(s.Id))
            .Select(s => new { s.Id, s.ImageUrl, Ten = s.Lounge.Name }).ToListAsync(ct);
        var theoId = anh.Concat(canh).ToDictionary(x => x.Id);
        items = items.Select(i => theoId.TryGetValue(i.TargetId, out var x) ? i with { TargetImageUrl = x.ImageUrl, LoungeName = x.Ten } : i).ToList();

        return new PaginatedResult<EventModerationDto>(items, page, pageSize, total);
    }

    public async Task<PaginatedResult<PendingLoungeShowDto>> GetPendingShowsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query =
            from m in _ctx.EventModerations.AsNoTracking()
            where m.TargetType == ModerationTargetType.Show && m.AdminDecision == null
            join s in _ctx.LoungeShows.AsNoTracking()
                on m.TargetId equals s.Id
            // Phong thu truoc lech trang thai neu 1 trong 2 ban ghi bi cap nhat rieng le —
            // AdminDecision == null thuong dong nghia Pending, nhung khong dua vao gia dinh do.
            where s.Status == LoungeShowStatus.Pending
            orderby m.AiScore descending, m.Id
            select new { m, s, s.Lounge.Name };

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PendingLoungeShowDto(
                x.s.Id,
                x.s.Name,
                // Xem LoungeShowMappingExtensions.DisplayImageUrl: CoverImageUrl khong ai ghi,
                // anh that nam o PosterUrl. Rieng cho nay dang ngai hon ca — Admin duyet mot buoi
                // dien ma khong nhin thay anh cua no.
                x.s.CoverImageUrl ?? x.s.PosterUrl,
                x.Name,
                x.s.ScheduledStart,
                x.s.Format.ToString(),
                x.m.AiScore,
                x.m.RiskLevel == null ? null : x.m.RiskLevel.ToString(),
                x.m.FlagReason,
                x.m.CreatedAt,
                x.m.SlaDeadline))
            .ToListAsync(ct);

        return new PaginatedResult<PendingLoungeShowDto>(items, page, pageSize, total);
    }

    public async Task<EventModeration?> GetByTargetAsync(
        ModerationTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        return await _ctx.EventModerations
            .FirstOrDefaultAsync(m => m.TargetType == targetType && m.TargetId == targetId, ct);
    }
}
