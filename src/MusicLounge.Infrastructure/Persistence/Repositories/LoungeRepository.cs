using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common;
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
        string? city, int? ownerId, bool includeUnapproved, int page, int pageSize,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var query = _ctx.Lounges.AsNoTracking();

        // BR-01 (MLACP-307). Trước đây chỗ này không lọc trạng thái, nên một phòng trà vừa tạo —
        // chưa ai duyệt, giấy phép kinh doanh chưa ai đọc — nằm ngay trong danh sách công khai.
        // includeUnapproved chỉ bật khi Owner đang xem chính phòng trà của mình: giấu hồ sơ đang
        // chờ duyệt khỏi chính người nộp nó thì họ tưởng đã mất.
        if (!includeUnapproved)
            query = query.Where(l => VenueLifecycle.Operating.Contains(l.Status));

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
                AtmosphereName = l.Atmosphere != null ? l.Atmosphere.Name : null,
                l.OwnerId, l.Status
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
            galleryImages,
            lounge.OwnerId,
            lounge.Status.ToString());
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

    /// <summary>
    /// Hàng đợi duyệt hồ sơ phòng trà (BR-01, MLACP-307).
    ///
    /// Sắp xếp theo khoá chính tăng dần thay vì theo CreatedAt: hai thứ tự này trùng nhau vì Id là
    /// identity tăng dần, nhưng provider SQLite dùng trong test không ORDER BY được cột
    /// DateTimeOffset — cùng lớp vấn đề đã xử ở MLACP-306. Cũ nhất lên trước, vì hồ sơ chờ lâu nhất
    /// là hồ sơ cần xử trước.
    /// </summary>
    public async Task<PaginatedResult<VenueReviewItemDto>> GetReviewQueueAsync(
        LoungeStatus status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _ctx.Lounges.AsNoTracking().Where(l => l.Status == status);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id, l.Name, l.Status, l.OwnerId,
                OwnerName = l.Owner.FullName,
                OwnerEmail = l.Owner.Email,
                OwnerPhone = l.Owner.Phone,
                l.Address.Street, l.Address.Ward, l.Address.District, l.Address.City,
                l.PrimaryImageUrl,
                l.BusinessLicenseUrl,
                l.CreatedAt,
                l.StatusReviewedAt,
                l.StatusReviewNote
            })
            .ToListAsync(ct);

        var items = rows.Select(l => new VenueReviewItemDto(
                l.Id,
                l.Name,
                l.Status.ToString(),
                l.OwnerId,
                l.OwnerName,
                l.OwnerEmail,
                l.OwnerPhone,
                l.Street
                    + (string.IsNullOrEmpty(l.Ward) ? "" : ", " + l.Ward)
                    + (string.IsNullOrEmpty(l.District) ? "" : ", " + l.District)
                    + ", " + l.City,
                l.PrimaryImageUrl,
                !string.IsNullOrWhiteSpace(l.BusinessLicenseUrl),
                l.CreatedAt,
                l.StatusReviewedAt,
                l.StatusReviewNote))
            .ToList();

        return new PaginatedResult<VenueReviewItemDto>(items, page, pageSize, total);
    }

    public async Task<bool> IsFollowingAsync(int loungeId, int userId, CancellationToken ct = default)
        => await _ctx.Follows
            .AsNoTracking()
            .AnyAsync(f => f.LoungeId == loungeId && f.UserId == userId, ct);
}
