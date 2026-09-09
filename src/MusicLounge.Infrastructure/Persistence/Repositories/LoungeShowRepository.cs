using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Analytics.Common;
using MusicLounge.Application.Common;
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

    /// <summary>
    /// Bảng "đang được quan tâm". Cách chấm điểm và lý do chọn nó nằm ở <see cref="TrendingScorer"/>;
    /// ở đây chỉ là gom đúng tín hiệu để đưa vào chấm.
    ///
    /// Lấy cả lượt lưu vào danh sách quan tâm, thứ mà bảng cũ bỏ sót hoàn toàn: nó chỉ đọc nhật ký
    /// hành vi, mà "lưu lại" được ghi ở bảng riêng chứ không phải một hành động trong nhật ký. Đó là
    /// một trong những tín hiệu mạnh nhất — người ta chỉ lưu thứ mình định quay lại.
    /// </summary>
    public async Task<IReadOnlyList<ShowTags>> GetShowTagsAsync(
        IReadOnlyCollection<int> showIds, CancellationToken ct = default)
    {
        if (showIds.Count == 0) return [];

        var genres = await _ctx.Set<LoungeShowGenre>()
            .Where(g => showIds.Contains(g.LoungeShowId))
            .Select(g => new { g.LoungeShowId, g.GenreId })
            .ToListAsync(ct);

        var moods = await _ctx.Set<LoungeShowMood>()
            .Where(m => showIds.Contains(m.LoungeShowId))
            .Select(m => new { m.LoungeShowId, m.MoodId })
            .ToListAsync(ct);

        var atmospheres = await _ctx.Set<LoungeShowAtmosphere>()
            .Where(a => showIds.Contains(a.LoungeShowId))
            .Select(a => new { a.LoungeShowId, a.AtmosphereId })
            .ToListAsync(ct);

        var loungeByShow = await _ctx.LoungeShows
            .Where(s => showIds.Contains(s.Id))
            .Select(s => new { s.Id, s.LoungeId })
            .ToListAsync(ct);

        var genreLookup = genres.ToLookup(g => g.LoungeShowId, g => g.GenreId);
        var moodLookup = moods.ToLookup(m => m.LoungeShowId, m => m.MoodId);
        var atmosphereLookup = atmospheres.ToLookup(a => a.LoungeShowId, a => a.AtmosphereId);

        return loungeByShow
            .Select(s => new ShowTags(
                s.Id,
                s.LoungeId,
                genreLookup[s.Id].ToHashSet(),
                moodLookup[s.Id].ToHashSet(),
                atmosphereLookup[s.Id].ToHashSet()))
            .ToList();
    }

    public async Task<IReadOnlyList<LoungeShow>> GetRecentlyPublishedAsync(
        int limit, string? city, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var query = WithDetails()
            .Where(s => s.Status == LoungeShowStatus.Published
                     || s.Status == LoungeShowStatus.Ongoing);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(s => s.Lounge.Address.City == city);

        // Sap theo khoa chinh giam dan thay vi theo CreatedAt: hai thu tu nay trung nhau vi Id la
        // identity tang dan, nhung provider SQLite dung trong test khong ORDER BY duoc cot
        // DateTimeOffset — cung lop van de da xu o MLACP-306.
        var candidates = await query.OrderByDescending(s => s.Id).Take(limit * 3).ToListAsync(ct);

        // Buoi da dien xong nhung chua kip doi trang thai thi khong con la thu de gioi thieu.
        return candidates
            .Where(s => ShowSchedule.EffectiveEnd(s) >= now)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<LoungeShow>> GetTrendingAsync(
        int limit, string? city, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var query = WithDetails()
            .Where(s => s.Status == LoungeShowStatus.Published
                     || s.Status == LoungeShowStatus.Ongoing);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(s => s.Lounge.Address.City == city);

        // Was OrderByDescending(s => s.BehaviourLogs.Count(b => b.CreatedAt >= since)) directly in
        // the query — combining a DateTimeOffset comparison with a correlated Count subquery inside
        // an ORDER BY does not translate under the SQLite provider used in tests (same class of
        // issue as LoungeRepository.GetAllAsync/GetByIdAsync). This is the AI-recommendation
        // fallback path (GetRecommendedLoungeShowsQueryHandler falls back to trending for any user
        // without ai_consent, which is every user by default), and RecommendationsController had
        // zero test coverage before this session, so it went unnoticed. Trades an unbounded fetch
        // of candidate shows for a translatable query — a reasonable trade at this project's scale
        // (Published/Ongoing shows platform-wide, not an ever-growing historical table); revisit if
        // that set ever grows large enough to matter.
        var candidates = await query.ToListAsync(ct);
        if (candidates.Count == 0) return [];

        // Buổi diễn đã diễn xong nhưng chưa kịp được job chuyển trạng thái thì không còn là thứ
        // "đang được quan tâm" — không ai mua vé vào một buổi tối đã qua. Dùng chung cách hiểu giờ
        // kết thúc với toàn hệ thống.
        candidates = candidates.Where(s => ShowSchedule.EffectiveEnd(s) >= now).ToList();
        if (candidates.Count == 0) return [];

        var showIds = candidates.Select(s => s.Id).ToList();

        // Same SQLite-translation limitation noted throughout this codebase: combining a
        // Contains(showIds) predicate with a DateTimeOffset comparison in one query doesn't
        // translate — filter by Contains server-side, then the date client-side.
        // Ve da ban va luot luu quan tam la giao dich cua chinh nguoi dung, ton tai bat ke ho co
        // bat AiConsent hay khong. Nhat ky hanh vi thi khong: LogUserBehaviourJob bo qua moi nguoi
        // chua dong y, va mac dinh la chua. Bang cu chi doc nhat ky, nen tren thuc te no gan nhu
        // luon rong — mot bang xep hang khong co gi de xep.
        var purchases = await _ctx.Tickets
            .Where(t => showIds.Contains(t.ShowId)
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used))
            .Select(t => new { t.ShowId, t.Id, t.CreatedAt })
            .ToListAsync(ct);

        var saves = await _ctx.Wishlists
            .Where(w => showIds.Contains(w.LoungeShowId))
            .Select(w => new { w.LoungeShowId, w.UserId, w.CreatedAt })
            .ToListAsync(ct);

        var behaviour = await _ctx.BehaviourLogs
            .Where(b => showIds.Contains(b.LoungeShowId))
            .Select(b => new { b.LoungeShowId, b.UserId, b.Action, b.CreatedAt })
            .ToListAsync(ct);

        var eventsByShow = new Dictionary<int, List<TrendingEvent>>();

        void Add(int showId, TrendingEvent e)
        {
            if (!eventsByShow.TryGetValue(showId, out var list))
                eventsByShow[showId] = list = [];
            list.Add(e);
        }

        // Moi chiec ve la mot tin hieu rieng: dat bon cho cho ca nhom la muc quan tam khac han mua
        // mot ve di mot minh. Dung Id cua ve lam Actor nen buoc gop trung khong lam mat khac biet do.
        foreach (var t in purchases)
            Add(t.ShowId, new TrendingEvent($"ticket:{t.Id}", TrendingSignal.Purchase, t.CreatedAt));

        foreach (var w in saves)
            Add(w.LoungeShowId, new TrendingEvent($"user:{w.UserId}", TrendingSignal.Save, w.CreatedAt));

        foreach (var b in behaviour)
        {
            // PurchaseTicket trong nhat ky bi bo qua o day: cung mot lan mua da duoc dem tu bang ve
            // ben tren roi, dem lai lan nua la nhan doi trong so cho nhung nguoi co bat AiConsent.
            if (b.Action == BehaviourAction.PurchaseTicket) continue;

            if (TrendingScorer.FromBehaviour(b.Action) is TrendingSignal signal)
                Add(b.LoungeShowId, new TrendingEvent($"user:{b.UserId}", signal, b.CreatedAt));
        }

        var scoreByShowId = eventsByShow.ToDictionary(
            kv => kv.Key, kv => TrendingScorer.Score(kv.Value, now));

        // Buổi diễn chưa có tín hiệu nào thì điểm bằng 0, và giữa những buổi cùng 0 điểm thì xếp
        // theo buổi sắp diễn trước. Đó là thứ tự có ích thật cho người đang tìm chỗ đi tối nay —
        // khác hẳn bảng cũ vốn xếp buổi diễn XA NHẤT lên đầu khi hoà điểm.
        return candidates
            .OrderByDescending(s => scoreByShowId.GetValueOrDefault(s.Id))
            .ThenBy(s => s.ScheduledStart)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<LoungeShow>> GetRecommendedByIdsAsync(
        IReadOnlyList<int> showIds, CancellationToken ct = default)
        => await WithDetails()
            .Where(s => showIds.Contains(s.Id)
                && (s.Status == LoungeShowStatus.Published || s.Status == LoungeShowStatus.Ongoing))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LoungeShow>> GetSimilarAsync(
        int showId, int loungeId, IReadOnlyList<int> genreIds, int limit, CancellationToken ct = default)
    {
        var query = WithDetails()
            .Where(s => s.Id != showId
                && (s.Status == LoungeShowStatus.Published || s.Status == LoungeShowStatus.Ongoing)
                && (s.LoungeId == loungeId || s.Genres.Any(g => genreIds.Contains(g.GenreId))));

        // Materialize then sort/take client-side — same SQLite-translation caution used throughout
        // this file (combining the boolean "matches both criteria" expression with ScheduledStart
        // ordering in one query doesn't reliably translate under the test provider).
        var candidates = await query.ToListAsync(ct);
        if (candidates.Count == 0) return [];

        return candidates
            .OrderByDescending(s => s.LoungeId == loungeId && s.Genres.Any(g => genreIds.Contains(g.GenreId)))
            .ThenBy(s => s.ScheduledStart)
            .Take(limit)
            .ToList();
    }

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
            // Xem LoungeShowMappingExtensions.DisplayImageUrl — CoverImageUrl khong ai ghi.
            .Select(s => new LoungeShowSuggestionItem(s.Id, s.Name, s.CoverImageUrl ?? s.PosterUrl))
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
            // Sap theo khoa chinh thay vi cot thoi gian: SQLite (provider dung trong test) tu
            // choi ORDER BY tren DateTimeOffset, nen sap theo CreatedAt o tang database khien
            // endpoint nay khong the co test nao. Khoa tu tang va CreatedAt deu duoc ghi luc chen
            // nen thu tu trung nhau, va sap theo khoa con on dinh hon khi hai ban ghi trung mocs.
            .OrderByDescending(w => w.Id);

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
            // Xem ghi chu ve sap xep theo khoa chinh o GetWishlistByUserAsync. Nhanh nay la sap
            // xep MAC DINH cua tim kiem, nen truoc day duong tim kiem thong thuong nhat cua ca he
            // thong lai la duong khong the viet test.
            _ => query.OrderByDescending(s => s.Id)
        };
}
