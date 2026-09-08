using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class EventModerationRepository
    : Repository<EventModeration, int>, IEventModerationRepository
{
    private readonly ApplicationDbContext _ctx;

    public EventModerationRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<PaginatedResult<EventModerationDto>> GetPendingAsync(
        ModerationTargetType? targetType, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = _ctx.EventModerations
            .AsNoTracking()
            .Where(m => m.AdminDecision == null);

        if (targetType.HasValue)
            baseQuery = baseQuery.Where(m => m.TargetType == targetType.Value);

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
                x.s.CoverImageUrl,
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
        ModerationTargetType targetType, int targetId, CancellationToken ct = default)
    {
        return await _ctx.EventModerations
            .FirstOrDefaultAsync(m => m.TargetType == targetType && m.TargetId == targetId, ct);
    }
}
