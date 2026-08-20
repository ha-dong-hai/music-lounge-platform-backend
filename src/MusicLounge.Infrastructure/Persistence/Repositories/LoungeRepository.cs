using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class LoungeRepository : ILoungeRepository
{
    private readonly ApplicationDbContext _ctx;

    public LoungeRepository(ApplicationDbContext ctx) => _ctx = ctx;

    public async Task<PaginatedResult<LoungeListItemDto>> GetAllAsync(
        string? city, int? ownerId, int page, int pageSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var query = _ctx.Lounges.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(l => l.Address.City == city);

        if (ownerId.HasValue)
            query = query.Where(l => l.OwnerId == ownerId.Value);

        var total = await query.CountAsync(ct);
        var pageLounges = await query
            .OrderBy(l => l.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id, l.Name, l.PrimaryImageUrl, l.BusinessLicenseUrl, l.Model3DUrl, l.AreaLayoutImageUrl,
                l.Address.Street, l.Address.District, l.Address.City,
                FollowerCount = l.Follows.Count
            })
            .ToListAsync(ct);

        var upcomingCounts = await GetUpcomingActiveShowCountsAsync(
            pageLounges.Select(l => l.Id).ToList(), now, ct);

        var items = pageLounges.Select(l => new LoungeListItemDto(
                l.Id, l.Name, l.PrimaryImageUrl, l.BusinessLicenseUrl, l.Model3DUrl, l.AreaLayoutImageUrl,
                l.Street, l.District, l.City, l.FollowerCount,
                upcomingCounts.GetValueOrDefault(l.Id)))
            .ToList();

        return new PaginatedResult<LoungeListItemDto>(items, page, pageSize, total);
    }

    public async Task<LoungeDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var lounge = await _ctx.Lounges
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new
            {
                l.Id, l.Name, l.PrimaryImageUrl, l.Model3DUrl, l.AreaLayoutImageUrl,
                l.Address.Street, l.Address.Ward, l.Address.District, l.Address.City,
                l.Address.Latitude, l.Address.Longitude,
                FollowerCount = l.Follows.Count,
                l.Description,
                AtmosphereName = l.Atmosphere != null ? l.Atmosphere.Name : null
            })
            .FirstOrDefaultAsync(ct);
        if (lounge is null) return null;

        var upcomingCount = (await GetUpcomingActiveShowCountsAsync([id], now, ct)).GetValueOrDefault(id);

        var galleryImages = await _ctx.LoungeGalleryImages
            .AsNoTracking()
            .Where(g => g.LoungeId == id)
            .OrderBy(g => g.OrderIndex)
            .Select(g => new LoungeGalleryImageDto(g.Id, g.ImageUrl, g.Caption, g.OrderIndex))
            .ToListAsync(ct);

        return new LoungeDetailDto(
            lounge.Id,
            lounge.Name,
            lounge.PrimaryImageUrl,
            lounge.Model3DUrl,
            lounge.AreaLayoutImageUrl,
            lounge.Street,
            lounge.Ward,
            lounge.District,
            lounge.City,
            lounge.Street
                + (string.IsNullOrEmpty(lounge.Ward) ? "" : ", " + lounge.Ward)
                + (string.IsNullOrEmpty(lounge.District) ? "" : ", " + lounge.District)
                + ", " + lounge.City,
            lounge.Latitude,
            lounge.Longitude,
            lounge.FollowerCount,
            upcomingCount,
            null,
            lounge.Description,
            lounge.AtmosphereName,
            galleryImages);
    }

    /// <summary>
    /// Counts each lounge's upcoming Published/Ongoing shows. Was previously a correlated
    /// subquery (l.LoungeShows.Count(s => s.ScheduledStart > now && (status-or-status))) directly
    /// inside GetAllAsync/GetByIdAsync's projection — combining a DateTimeOffset comparison with
    /// an enum-equality OR inside one correlated Count subquery fails to translate under the
    /// SQLite provider used in tests (same class of issue documented elsewhere in this codebase),
    /// which meant GET /api/v1/lounges and GET /api/v1/lounges/{id} threw a 500 on every single
    /// call. Neither endpoint had any test coverage before this session, so this went unnoticed.
    /// Batching this as its own simple-predicate query, then filtering/grouping in memory, both
    /// fixes the translation failure and avoids a correlated-subquery-per-row execution shape.
    /// </summary>
    private async Task<Dictionary<int, int>> GetUpcomingActiveShowCountsAsync(
        IReadOnlyCollection<int> loungeIds, DateTimeOffset now, CancellationToken ct)
    {
        if (loungeIds.Count == 0) return [];

        var candidates = await _ctx.LoungeShows
            .Where(s => loungeIds.Contains(s.LoungeId)
                && (s.Status == LoungeShowStatus.Published || s.Status == LoungeShowStatus.Ongoing))
            .Select(s => new { s.LoungeId, s.ScheduledStart })
            .ToListAsync(ct);

        return candidates
            .Where(s => s.ScheduledStart > now)
            .GroupBy(s => s.LoungeId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<bool> IsFollowingAsync(int loungeId, int userId, CancellationToken ct = default)
        => await _ctx.Follows
            .AsNoTracking()
            .AnyAsync(f => f.LoungeId == loungeId && f.UserId == userId, ct);
}
