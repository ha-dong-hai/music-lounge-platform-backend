using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class LoungeShowRepository : Repository<LoungeShow, int>, ILoungeShowRepository
{
    private readonly ApplicationDbContext _ctx;

    public LoungeShowRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    private IQueryable<LoungeShow> WithDetails()
        => _ctx.LoungeShows
            .AsNoTracking()
            // 3 sibling collection Includes (Genres, Performances, TicketTiers.Prices) without this
            // multiply into a cartesian-product single query — row count grows with
            // genres×performers×tiers×prices per show. Split into separate queries instead.
            .AsSplitQuery()
            .Include(s => s.Lounge)
            .Include(s => s.Genres).ThenInclude(g => g.Genre)
            .Include(s => s.Performances).ThenInclude(p => p.Performer)
            .Include(s => s.TicketTiers).ThenInclude(t => t.Prices);

    public async Task<LoungeShow?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
        // Moods/Atmospheres/Lounge.Atmosphere/Ratings.User chi can cho trang chi tiet 1 show —
        // khong them vao WithDetails() dung chung, tranh cac danh sach (Search/GetPublished/...)
        // phai ganh them join khong dung toi.
        => await WithDetails()
            .Include(s => s.Moods).ThenInclude(m => m.Mood)
            .Include(s => s.Atmospheres).ThenInclude(a => a.Atmosphere)
            .Include(s => s.Lounge).ThenInclude(l => l.Atmosphere)
            .Include(s => s.Ratings).ThenInclude(r => r.User)
            .Include(s => s.Livestream)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<PaginatedResult<LoungeShow>> GetPublishedAsync(
        int page, int pageSize, LoungeShowSortBy sortBy,
        bool includeSoldOut, CancellationToken ct = default)
    {
        var query = WithDetails()
            .Where(s => s.Status == LoungeShowStatus.Published
                     || s.Status == LoungeShowStatus.Ongoing);

        if (!includeSoldOut)
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(s => s.TicketTiers.Any(tier =>
                tier.Prices.Any(price =>
                    !price.Quota.HasValue
                    || price.Quota.Value > (
                        _ctx.Tickets.Count(t =>
                            t.PriceId == price.Id &&
                            (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Pending))
                        + (_ctx.TicketHolds
                            .Where(h => h.PriceId == price.Id && h.ExpiresAt > now)
                            .Sum(h => (int?)h.Quantity) ?? 0))
                )
            ));
        }

        return await SortAndPaginateAsync(query, sortBy, page, pageSize, ct);
    }

    public async Task<PaginatedResult<LoungeShow>> GetMineAsync(
        int ownerId, int page, int pageSize, LoungeShowSortBy sortBy,
        LoungeShowStatus? status = null, CancellationToken ct = default)
    {
        var query = WithDetails().Where(s => s.Lounge.OwnerId == ownerId);
        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);
        return await SortAndPaginateAsync(query, sortBy, page, pageSize, ct);
    }

    public async Task<PaginatedResult<LoungeShow>> SearchAsync(
        LoungeShowSearchParams p, CancellationToken ct = default)
    {
        // MLACP-58 DONE WHEN: "chi hien thi su kien da duoc duyet cong khai" — truoc day chi loai
        // Draft, de lot Pending (dang cho Admin duyet, chua cong khai) vao ket qua tim kiem cong
        // khai. Loai them Pending o day; Ended/Cancelled van do rieng IncludeEnded ben duoi quyet dinh.
        var query = WithDetails()
            .Where(s => s.Status != LoungeShowStatus.Draft && s.Status != LoungeShowStatus.Pending);

        if (!string.IsNullOrWhiteSpace(p.Keyword))
            // Contains() (khong phai EF.Functions.Like voi chuoi noi truoc trong C#) — SQL Server
            // coi literal khong co tien to N la non-Unicode, lam sai lech dau tieng Viet khi so
            // sanh (LIKE '%Nhạc%' khong khop du du lieu luu dung NVARCHAR); Contains() luon
            // parameterize dung kieu NVARCHAR, khop chinh xac ky tu co dau.
            query = query.Where(s => s.Name.Contains(p.Keyword) || s.Description.Contains(p.Keyword));

        if (p.GenreIds is { Length: > 0 })
            query = query.Where(s => s.Genres.Any(g => p.GenreIds.Contains(g.GenreId)));

        if (p.MoodIds is { Length: > 0 })
            query = query.Where(s => s.Moods.Any(m => p.MoodIds.Contains(m.MoodId)));

        if (p.AtmosphereIds is { Length: > 0 })
            query = query.Where(s => s.Atmospheres.Any(a => p.AtmosphereIds.Contains(a.AtmosphereId)));

        if (p.PerformerId.HasValue)
            query = query.Where(s => s.Performances.Any(perf => perf.PerformerId == p.PerformerId));

        if (p.LoungeId.HasValue)
            query = query.Where(s => s.LoungeId == p.LoungeId);

        if (!string.IsNullOrWhiteSpace(p.City))
            query = query.Where(s => s.Lounge.Address.City == p.City);

        if (!string.IsNullOrWhiteSpace(p.District))
            query = query.Where(s => s.Lounge.Address.District == p.District);

        if (!string.IsNullOrWhiteSpace(p.Ward))
            query = query.Where(s => s.Lounge.Address.Ward == p.Ward);

        if (p.DateFrom.HasValue)
            query = query.Where(s => s.ScheduledStart >= p.DateFrom.Value);

        if (p.DateTo.HasValue)
        {
            // DateTo tu bo loc/lich chon ngay o frontend luon la 1 ngay-lich (khong kem gio), vd
            // "2026-08-15" -> parse thanh DateTimeOffset dung nghia 00:00:00 ngay do. So sanh
            // ScheduledStart <= p.DateTo.Value se loai bo dung moi show dien buoi toi CUNG NGAY
            // (vd 19:30) vi 19:30 > 00:00:00 — bao gom het toan bo ngay-lich do bang cach lay moc
            // 00:00:00 ngay hom sau (cung offset voi gia tri goc) lam can tren khong bao gom.
            var exclusiveUpperBound = new DateTimeOffset(p.DateTo.Value.Date.AddDays(1), p.DateTo.Value.Offset);
            query = query.Where(s => s.ScheduledStart < exclusiveUpperBound);
        }

        if (p.Format.HasValue)
            query = query.Where(s => s.Format == p.Format.Value);

        if (p.MinPrice.HasValue)
            query = query.Where(s => s.TicketTiers.Any(t =>
                t.Prices.Any(pr => pr.Price >= p.MinPrice.Value)));

        if (p.MaxPrice.HasValue)
            query = query.Where(s => s.TicketTiers.Any(t =>
                t.Prices.Any(pr => pr.Price <= p.MaxPrice.Value)));

        if (!p.IncludeEnded)
            query = query.Where(s => s.Status != LoungeShowStatus.Ended
                                  && s.Status != LoungeShowStatus.Cancelled);

        if (!p.IncludeSoldOut)
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(s => s.TicketTiers.Any(tier =>
                tier.Prices.Any(price =>
                    !price.Quota.HasValue
                    || price.Quota.Value > (
                        _ctx.Tickets.Count(t =>
                            t.PriceId == price.Id &&
                            (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Pending))
                        + (_ctx.TicketHolds
                            .Where(h => h.PriceId == price.Id && h.ExpiresAt > now)
                            .Sum(h => (int?)h.Quantity) ?? 0))
                )
            ));
        }

        return await SortAndPaginateAsync(query, p.SortBy, p.Page, p.PageSize, ct);
    }

    public async Task<PaginatedResult<LoungeShow>> GetByPerformerAsync(
        int performerId, bool includeEnded, int page, int pageSize, CancellationToken ct = default)
    {
        var query = WithDetails()
            .Where(s => s.Performances.Any(p => p.PerformerId == performerId)
                && s.Status != LoungeShowStatus.Draft);

        if (!includeEnded)
            query = query.Where(s => s.Status != LoungeShowStatus.Ended
                                  && s.Status != LoungeShowStatus.Cancelled);

        // ORDER BY ScheduledStart (DateTimeOffset) combined with Skip/Take in one deferred query
        // does not translate under the SQLite provider used in tests — same limitation as
        // GetTrendingAsync/GetWishlistByUserAsync elsewhere in this file. Materialize the
        // (already Status-filtered) set, then sort/paginate client-side.
        var candidates = await query.ToListAsync(ct);
        var total = candidates.Count;
        var items = candidates
            .OrderByDescending(s => s.ScheduledStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedResult<LoungeShow>(items, page, pageSize, total);
    }

    public async Task<PaginatedResult<LoungeShow>> GetByLoungeAsync(
        int loungeId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = WithDetails()
            .Where(s => s.LoungeId == loungeId && s.Status != LoungeShowStatus.Draft);

        // Same ORDER BY-on-DateTimeOffset-with-Skip/Take limitation as GetByPerformerAsync above.
        var candidates = await query.ToListAsync(ct);
        var total = candidates.Count;
        var items = candidates
            .OrderByDescending(s => s.ScheduledStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedResult<LoungeShow>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<LoungeShow>> GetTrendingAsync(
        int limit, string? city, CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-7);
        var query = WithDetails()
            .Where(s => s.Status == LoungeShowStatus.Published
                     || s.Status == LoungeShowStatus.Ongoing);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(s => s.Lounge.Address.City == city);

        // Was OrderByDescending(s => s.BehaviourLogs.Count(b => b.CreatedAt >= since)) directly in
        // the query — combining a DateTimeOffset comparison with a correlated Count subquery inside
        // an ORDER BY does not translate under the SQLite provider used in tests (same class of
        // issue as LoungeRepository.GetAllAsync/GetByIdAsync, fixed earlier in this session). This
        // is the AI-recommendation fallback path (GetRecommendedLoungeShowsQueryHandler falls back
        // to trending for any user without ai_consent, which is every user by default), and
        // RecommendationsController had zero test coverage before this session, so it went
        // unnoticed. Trades an unbounded fetch of candidate shows for a translatable query — a
        // reasonable trade at this project's scale (Published/Ongoing shows platform-wide, not an
        // ever-growing historical table); revisit if that set ever grows large enough to matter.
        var candidates = await query.ToListAsync(ct);
        if (candidates.Count == 0) return [];

        var showIds = candidates.Select(s => s.Id).ToList();
        // Same SQLite-translation limitation noted throughout this codebase: combining a
        // Contains(showIds) predicate with a DateTimeOffset comparison in one query doesn't
        // translate — filter by Contains server-side, then the date client-side.
        var logsForCandidates = await _ctx.BehaviourLogs
            .Where(b => showIds.Contains(b.LoungeShowId))
            .Select(b => new { b.LoungeShowId, b.CreatedAt })
            .ToListAsync(ct);
        var countsByShowId = logsForCandidates
            .Where(b => b.CreatedAt >= since)
            .GroupBy(b => b.LoungeShowId)
            .ToDictionary(g => g.Key, g => g.Count());

        return candidates
            .OrderByDescending(s => countsByShowId.GetValueOrDefault(s.Id))
            .ThenByDescending(s => s.ScheduledStart)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<LoungeShow>> GetRecommendedByIdsAsync(
        IReadOnlyList<int> showIds, CancellationToken ct = default)
        => await WithDetails()
            .Where(s => showIds.Contains(s.Id)
                && (s.Status == LoungeShowStatus.Published || s.Status == LoungeShowStatus.Ongoing))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetDistinctCitiesAsync(CancellationToken ct = default)
        => await _ctx.Lounges
            .AsNoTracking()
            .Select(l => l.Address.City)
            .Where(c => c != string.Empty)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LoungeShowSuggestionItem>> GetSuggestionsAsync(
        string keyword, int limit, CancellationToken ct = default)
    {
        return await _ctx.LoungeShows
            .AsNoTracking()
            .Where(s => (s.Status == LoungeShowStatus.Published || s.Status == LoungeShowStatus.Ongoing)
                     && s.Name.Contains(keyword))
            .OrderBy(s => s.Name)
            .Take(limit)
            .Select(s => new LoungeShowSuggestionItem(s.Id, s.Name, s.CoverImageUrl))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlySet<int>> GetWishlistedShowIdsAsync(
        int userId, CancellationToken ct = default)
    {
        var ids = await _ctx.Wishlists
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .Select(w => w.LoungeShowId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    public async Task<PaginatedResult<LoungeShow>> GetWishlistByUserAsync(
        int userId, int page, int pageSize, CancellationToken ct = default)
    {
        // Include() cannot follow a Select() that projects through a navigation
        // (w => w.LoungeShow) — EF Core loses track of the root entity type.
        // Page the wishlist ordering first, then fetch the shows (with Include)
        // by id and re-apply the order client-side.
        var wishlistQuery = _ctx.Wishlists
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt);

        var total = await wishlistQuery.CountAsync(ct);

        var orderedShowIds = await wishlistQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => w.LoungeShowId)
            .ToListAsync(ct);

        var shows = await WithDetails()
            .Where(s => orderedShowIds.Contains(s.Id))
            .ToListAsync(ct);

        var items = orderedShowIds
            .Select(id => shows.First(s => s.Id == id))
            .ToList();

        return new PaginatedResult<LoungeShow>(items, page, pageSize, total);
    }

    public async Task<int?> GetLoungeOwnerIdAsync(int showId, CancellationToken ct = default)
        => await _ctx.LoungeShows
            .AsNoTracking()
            .Where(s => s.Id == showId)
            .Select(s => (int?)s.Lounge.OwnerId)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<int, int>> GetSoldAndHeldCountsByPriceAsync(
        IReadOnlyList<int> priceIds, CancellationToken ct = default)
    {
        if (priceIds.Count == 0) return new Dictionary<int, int>();

        var ticketCounts = await _ctx.Tickets
            .AsNoTracking()
            .Where(t => priceIds.Contains(t.PriceId)
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Pending))
            .GroupBy(t => t.PriceId)
            .Select(g => new { PriceId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Filter by PriceId server-side, then filter/group the (typically small) result
        // client-side — combining a Contains(list) predicate with a DateTimeOffset
        // comparison in one query fails to translate under the SQLite provider used in tests.
        var activeHolds = await _ctx.TicketHolds
            .AsNoTracking()
            .Where(h => priceIds.Contains(h.PriceId))
            .ToListAsync(ct);

        var holdCounts = activeHolds
            .Where(h => h.ExpiresAt > DateTimeOffset.UtcNow)
            .GroupBy(h => h.PriceId)
            .Select(g => new { PriceId = g.Key, Total = g.Sum(h => h.Quantity) })
            .ToList();

        var result = priceIds.ToDictionary(id => id, _ => 0);
        foreach (var row in ticketCounts) result[row.PriceId] += row.Count;
        foreach (var row in holdCounts) result[row.PriceId] += row.Total;
        return result;
    }

    // sortBy == StartingSoon combines a Where(ScheduledStart > now) with the query's existing
    // Status filter and/or an ORDER BY on ScheduledStart with Skip/Take — none of which translate
    // under the SQLite provider used in tests (same DateTimeOffset limitation as GetByLoungeAsync/
    // GetByPerformerAsync above). Every other sort mode stays a normal single DB round trip; only
    // StartingSoon materializes the (already otherwise-filtered) candidate set and finishes the
    // filter/sort/paginate client-side.
    private static async Task<PaginatedResult<LoungeShow>> SortAndPaginateAsync(
        IQueryable<LoungeShow> query, LoungeShowSortBy sortBy, int page, int pageSize, CancellationToken ct)
    {
        if (sortBy == LoungeShowSortBy.StartingSoon)
        {
            var candidates = await query.ToListAsync(ct);
            var upcoming = candidates
                .Where(s => s.ScheduledStart > DateTimeOffset.UtcNow)
                .OrderBy(s => s.ScheduledStart)
                .ToList();
            var candidateTotal = upcoming.Count;
            var candidateItems = upcoming.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PaginatedResult<LoungeShow>(candidateItems, page, pageSize, candidateTotal);
        }

        var sorted = ApplySort(query, sortBy);
        var total = await sorted.CountAsync(ct);
        var items = await sorted.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PaginatedResult<LoungeShow>(items, page, pageSize, total);
    }

    // LoungeShowSortBy.StartingSoon is intercepted by SortAndPaginateAsync before this is ever
    // called (see comment there) — never reaches the default branch below for that sort mode.
    private static IQueryable<LoungeShow> ApplySort(
        IQueryable<LoungeShow> query, LoungeShowSortBy sortBy)
        => sortBy switch
        {
            LoungeShowSortBy.Popular => query.OrderByDescending(
                s => s.BehaviourLogs.Count),
            LoungeShowSortBy.PriceAsc => query.OrderBy(
                s => s.TicketTiers.SelectMany(t => t.Prices)
                     .Min(p => (decimal?)p.Price)),
            LoungeShowSortBy.PriceDesc => query.OrderByDescending(
                s => s.TicketTiers.SelectMany(t => t.Prices)
                     .Max(p => (decimal?)p.Price)),
            _ => query.OrderByDescending(s => s.CreatedAt)
        };
}
