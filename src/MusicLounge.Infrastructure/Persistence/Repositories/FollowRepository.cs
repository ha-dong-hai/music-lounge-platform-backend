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
            // Sap theo khoa chinh thay vi cot thoi gian: SQLite (provider dung trong test) tu
            // choi ORDER BY tren DateTimeOffset, nen sap theo CreatedAt o tang database khien
            // endpoint nay khong the co test nao. Khoa tu tang va CreatedAt deu duoc ghi luc chen
            // nen thu tu trung nhau, va sap theo khoa con on dinh hon khi hai ban ghi trung mocs.
            .OrderByDescending(f => f.Id);

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
