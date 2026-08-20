using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Follows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class FollowRepository : Repository<Follow, int>, IFollowRepository
{
    private readonly ApplicationDbContext _ctx;

    public FollowRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<PaginatedResult<FollowedLoungeDto>> GetFollowedLoungesByUserAsync(
        int userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _ctx.Follows
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FollowedLoungeDto(
                f.Lounge.Id,
                f.Lounge.Name,
                f.Lounge.PrimaryImageUrl,
                f.Lounge.Address.District,
                f.Lounge.Address.City,
                f.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<FollowedLoungeDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<int>> GetFollowerUserIdsAsync(int loungeId, CancellationToken ct = default)
        => await _ctx.Follows
            .AsNoTracking()
            .Where(f => f.LoungeId == loungeId)
            .Select(f => f.UserId)
            .ToListAsync(ct);
}
