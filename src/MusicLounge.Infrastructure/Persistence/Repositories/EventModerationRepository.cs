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
                m.ReviewedAt))
            .ToListAsync(ct);

        return new PaginatedResult<EventModerationDto>(items, page, pageSize, total);
    }

    public async Task<EventModeration?> GetByTargetAsync(
        ModerationTargetType targetType, int targetId, CancellationToken ct = default)
    {
        return await _ctx.EventModerations
            .FirstOrDefaultAsync(m => m.TargetType == targetType && m.TargetId == targetId, ct);
    }
}
