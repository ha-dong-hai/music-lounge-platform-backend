using MediatR;
using MusicLounge.Application.Analytics.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Queries.GetRecommendedLoungeShows;

/// <summary>
/// Gợi ý buổi diễn cho bất kỳ ai đang hỏi — kể cả khách chưa đăng nhập.
///
/// <b>Trước đây có hai lỗ hổng, và cả hai đều khiến cá nhân hoá gần như không tới được ai.</b>
///
/// Thứ nhất, endpoint này bắt buộc đăng nhập, nên khách vãng lai nhận 401. Mà khách vãng lai chính
/// là người cần được thuyết phục nhất: họ chưa có lý do gì để tin nền tảng này.
///
/// Thứ hai — nghiêm trọng hơn — người dùng đã đăng nhập mà chưa bật đồng ý AI cũng chỉ nhận đúng
/// bảng đang thịnh hành, giống hệt mọi người khác. Kể cả khi họ vừa đi qua bước onboarding và tự
/// tay chọn "tôi thích Bolero, acoustic, không gian ấm". Hệ thống có sở thích đó trong tay và cố
/// tình không dùng.
///
/// <b>Ranh giới đúng của sự đồng ý không nằm ở chỗ đó.</b> Đồng ý AI là để chặn việc hệ thống SUY
/// ĐOÁN ra con người bạn từ hành vi theo dõi được — đó mới là lập hồ sơ người dùng. Còn tôn trọng
/// một sở thích người dùng TỰ KHAI, hay tính đến phòng trà họ chủ động bấm theo dõi, thì đơn giản
/// là làm đúng việc họ vừa yêu cầu. Bắt họ đồng ý cho phân tích hành vi mới được nhận thứ họ vừa
/// gõ vào là đánh tráo hai chuyện khác nhau.
///
/// <b>Ba đường, xếp theo lượng thông tin có được về người hỏi:</b>
///
/// 1. Đã đăng nhập, đã bật đồng ý AI, và có kết quả tính sẵn còn hạn → dùng kết quả đó. Đây là
///    đường đầy đủ nhất, có cả lọc cộng tác ML.NET và lời giải thích do AI viết.
/// 2. Đã đăng nhập → xếp theo sở thích tự khai và phòng trà đang theo dõi, tính ngay trong request.
///    Nếu có bật đồng ý AI thì đồng thời đặt lịch tính lại nền cho lần sau.
/// 3. Chưa đăng nhập → xếp theo ngữ cảnh chính request đó mang theo. Không lưu gì lại.
///
/// Cả ba đường dùng CHUNG một công thức chấm điểm (<see cref="TasteMatcher"/>), nên cùng một người
/// không nhận hai thứ tự khác nhau chỉ vì kết quả đến từ cache hay vừa được tính.
/// </summary>
internal sealed class GetRecommendedLoungeShowsQueryHandler
    : IRequestHandler<GetRecommendedLoungeShowsQuery, IReadOnlyList<RecommendedLoungeShowDto>>
{
    /// <summary>
    /// Số buổi diễn lấy ra làm ứng viên trước khi xếp theo gu. Rộng hơn hẳn số cuối cùng trả về,
    /// vì buổi diễn hợp gu nhất chưa chắc nằm trong nhóm được quan tâm nhất — lấy đúng bằng
    /// <c>limit</c> thì việc xếp lại theo gu gần như không còn đổi được gì.
    /// </summary>
    private const int CandidatePoolSize = 60;

    /// <summary>
    /// Trần số buổi diễn khách vãng lai được gửi lên làm ngữ cảnh. Đủ cho lịch sử xem gần đây thật
    /// sự, và chặn việc gửi lên một danh sách khổng lồ để bắt máy chủ làm việc thay.
    /// </summary>
    private const int MaxGuestContextShows = 20;

    private readonly IRepository<AiRecommendation, int> _recRepo;
    private readonly IRepository<User, int> _userRepo;
    private readonly IRepository<UserFavouriteGenre, int> _genreRepo;
    private readonly IRepository<UserFavouriteMood, int> _moodRepo;
    private readonly IRepository<UserFavouriteAtmosphere, int> _atmosphereRepo;
    private readonly IRepository<Follow, int> _followRepo;
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobService _jobs;
    private readonly IRepository<Ticket, Guid> _ticketRepo;
    private readonly IRepository<ShowWishlist, int> _wishlistRepo;

    public GetRecommendedLoungeShowsQueryHandler(
        IRepository<AiRecommendation, int> recRepo,
        IRepository<User, int> userRepo,
        IRepository<UserFavouriteGenre, int> genreRepo,
        IRepository<UserFavouriteMood, int> moodRepo,
        IRepository<UserFavouriteAtmosphere, int> atmosphereRepo,
        IRepository<Follow, int> followRepo,
        ILoungeShowRepository showRepo,
        ICurrentUserService currentUser,
        IBackgroundJobService jobs,
        IRepository<Ticket, Guid> ticketRepo,
        IRepository<ShowWishlist, int> wishlistRepo)
    {
        _recRepo = recRepo;
        _userRepo = userRepo;
        _genreRepo = genreRepo;
        _moodRepo = moodRepo;
        _atmosphereRepo = atmosphereRepo;
        _followRepo = followRepo;
        _showRepo = showRepo;
        _currentUser = currentUser;
        _jobs = jobs;
        _ticketRepo = ticketRepo;
        _wishlistRepo = wishlistRepo;
    }

    public async Task<IReadOnlyList<RecommendedLoungeShowDto>> Handle(
        GetRecommendedLoungeShowsQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 50);

        if (!_currentUser.IsAuthenticated)
            return await ForGuestAsync(request, limit, ct);

        var user = await _userRepo.GetByIdAsync(_currentUser.UserId, ct);
        if (user is null)
            return await ForGuestAsync(request, limit, ct);

        if (user.AiConsent)
        {
            // Same recurring SQLite-translation limitation documented throughout this codebase:
            // combining an equality predicate with a DateTimeOffset comparison in one Where clause
            // fails to translate under the test provider — filter server-side on the simple
            // equality, then the expiry client-side.
            var now = DateTimeOffset.UtcNow;
            var allForUser = await _recRepo.FindAsync(r => r.UserId == _currentUser.UserId, ct);
            var cached = allForUser.Where(r => r.ExpiresAt > now).ToList();

            if (cached.Count > 0)
                return await FromCacheAsync(cached, limit, await AlreadyHasAsync(ct), ct);

            // Chưa có kết quả tính sẵn: đặt lịch tính nền cho lần sau, còn lần này vẫn phải trả về
            // thứ dùng được ngay. Trước đây chỗ này trả về bảng thịnh hành chung; giờ ít nhất cũng
            // xếp theo sở thích người dùng đã khai.
            _jobs.EnqueueRecommendationRefresh(_currentUser.UserId);
        }

        var taste = await DeclaredTasteAsync(_currentUser.UserId, ct);
        return await RankByTasteAsync(
            taste, request.City, limit,
            reasonWhenMatched: "Hợp với sở thích bạn đã chọn",
            await AlreadyHasAsync(ct),
            ct);
    }

    /// <summary>
    /// Gu suy ra từ chính request khách vãng lai gửi lên. Không đọc và không ghi hồ sơ nào của họ,
    /// vì họ không có hồ sơ nào để đọc — và không được tạo ra một cái sau lưng họ.
    /// </summary>
    private async Task<IReadOnlyList<RecommendedLoungeShowDto>> ForGuestAsync(
        GetRecommendedLoungeShowsQuery request, int limit, CancellationToken ct)
    {
        var genreIds = (request.GenreIds ?? []).ToHashSet();
        var moodIds = new HashSet<int>();
        var atmosphereIds = new HashSet<int>();

        var recent = (request.RecentShowIds ?? []).Distinct().Take(MaxGuestContextShows).ToList();
        if (recent.Count > 0)
        {
            // "Vì bạn vừa xem" — suy gu từ thẻ phân loại của chính những buổi diễn đó. Đây là cách
            // cá nhân hoá cho khách vãng lai mà không cần biết họ là ai: thông tin đến trong
            // request, dùng xong thì hết.
            foreach (var tags in await _showRepo.GetShowTagsAsync(recent, ct))
            {
                genreIds.UnionWith(tags.GenreIds);
                moodIds.UnionWith(tags.MoodIds);
                atmosphereIds.UnionWith(tags.AtmosphereIds);
            }
        }

        var taste = new TasteProfile(genreIds, moodIds, atmosphereIds, new HashSet<int>());

        // Khách vãng lai không có gì để loại trừ: hệ thống không biết họ là ai, nên cũng không
        // biết họ đã mua vé buổi nào — và không được đi tìm hiểu.
        return await RankByTasteAsync(
            taste, request.City, limit,
            reasonWhenMatched: recent.Count > 0
                ? "Giống những buổi diễn bạn vừa xem"
                : "Hợp với thể loại bạn đang tìm",
            alreadyHas: new HashSet<int>(),
            ct);
    }

    /// <summary>
    /// Những buổi diễn người này đã có rồi: đã mua vé, hoặc đã lưu vào danh sách quan tâm.
    ///
    /// Gợi ý sinh ra để giúp KHÁM PHÁ. Giới thiệu lại buổi diễn người ta vừa mua vé là điều ngược
    /// hẳn với mục đích đó, và tệ hơn là nó chiếm mất chỗ của thứ họ chưa tìm thấy. Buổi đã lưu
    /// quan tâm cũng vậy — họ tự tìm ra rồi, và đã có màn hình riêng cho danh sách đó.
    ///
    /// Đọc ngay lúc trả kết quả chứ không lúc tính sẵn: mua vé xong là buổi đó biến khỏi gợi ý
    /// ngay, không phải đợi hết 6 tiếng cache.
    /// </summary>
    /// <summary>
    /// Không để một phòng trà chiếm trọn danh sách gợi ý.
    ///
    /// Danh sách được xếp thuần theo mức hợp gu, nên một phòng trà đăng mười buổi diễn cùng thể
    /// loại sẽ lấp kín toàn bộ danh sách của người thích thể loại đó. Người dùng mở màn hình gợi ý
    /// ra và tưởng nền tảng chỉ có mỗi chỗ đó — đúng vấn đề mà các sàn thương mại điện tử gọi là
    /// một người bán chiếm hết kết quả, và họ xử bằng cách đặt trần tỉ lệ theo người bán.
    ///
    /// Ở đây có hai bên cùng được lợi: khán giả thấy được nhiều lựa chọn hơn, và các phòng trà nhỏ
    /// không bị đẩy khỏi màn hình khám phá chỉ vì đăng ít buổi diễn hơn.
    ///
    /// Trần là <c>max(2, limit/3)</c> — không ai chiếm quá khoảng một phần ba danh sách, nhưng luôn
    /// được ít nhất hai suất để danh sách ngắn không bị siết quá tay.
    /// </summary>
    private static List<T> CapPerVenue<T>(
        IReadOnlyList<T> ordered, int limit, Func<T, int> venueId)
    {
        var cap = Math.Max(2, limit / 3);

        var kept = new List<T>();
        var overflow = new List<T>();
        var takenByVenue = new Dictionary<int, int>();

        foreach (var item in ordered)
        {
            var venue = venueId(item);
            var taken = takenByVenue.GetValueOrDefault(venue);

            if (taken < cap)
            {
                takenByVenue[venue] = taken + 1;
                kept.Add(item);
            }
            else
            {
                overflow.Add(item);
            }
        }

        // Cung nguyen tac nhu PreferUnseen: tran chi de sap xep lai, khong duoc lam danh sach ngan
        // di. Khong du thi bu bang phan bi tran, giu nguyen thu tu cu.
        if (kept.Count >= limit) return kept;
        return [.. kept, .. overflow];
    }

    /// <summary>
    /// Đẩy những gì người dùng đã có xuống cuối thay vì cắt hẳn.
    ///
    /// Cắt hẳn là câu trả lời đúng khi kho đủ lớn. Nhưng nền tảng này hiện chỉ có vài buổi diễn
    /// đang mở bán, nên cắt hẳn sẽ trả về danh sách rỗng — và một danh sách rỗng còn tệ hơn một
    /// danh sách có thứ hơi thừa. Nên: ưu tiên thứ chưa thấy, chỉ bù bằng thứ đã có khi không còn
    /// gì khác để hiện.
    /// </summary>
    private static List<T> PreferUnseen<T>(
        IReadOnlyList<T> ordered, IReadOnlySet<int> alreadyHas, int limit, Func<T, int> showId)
    {
        if (alreadyHas.Count == 0) return ordered.ToList();

        var unseen = ordered.Where(x => !alreadyHas.Contains(showId(x))).ToList();
        if (unseen.Count >= limit) return unseen;

        var seen = ordered.Where(x => alreadyHas.Contains(showId(x)));
        return [.. unseen, .. seen];
    }

    private async Task<HashSet<int>> AlreadyHasAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId;

        var tickets = await _ticketRepo.FindAsync(
            t => t.BuyerId == userId
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used), ct);

        var saved = await _wishlistRepo.FindAsync(w => w.UserId == userId, ct);

        return tickets.Select(t => t.ShowId).Concat(saved.Select(w => w.LoungeShowId)).ToHashSet();
    }

    /// <summary>
    /// Sở thích người dùng tự khai ở bước onboarding, cộng phòng trà họ đang theo dõi. Cố ý KHÔNG
    /// đọc nhật ký hành vi: đó là phần cần sự đồng ý, và nó đã được dùng ở đường tính sẵn.
    /// </summary>
    private async Task<TasteProfile> DeclaredTasteAsync(int userId, CancellationToken ct)
    {
        var genres = await _genreRepo.FindAsync(g => g.UserId == userId, ct);
        var moods = await _moodRepo.FindAsync(m => m.UserId == userId, ct);
        var atmospheres = await _atmosphereRepo.FindAsync(a => a.UserId == userId, ct);
        var follows = await _followRepo.FindAsync(f => f.UserId == userId, ct);

        return new TasteProfile(
            genres.Select(g => g.GenreId).ToHashSet(),
            moods.Select(m => m.MoodId).ToHashSet(),
            atmospheres.Select(a => a.AtmosphereId).ToHashSet(),
            follows.Select(f => f.LoungeId).ToHashSet());
    }

    /// <summary>
    /// Lấy tập ứng viên từ bảng đang được quan tâm rồi xếp lại theo gu. Buổi diễn không khớp gì vẫn
    /// nằm trong danh sách, ở phía dưới và giữ nguyên thứ tự thịnh hành — cá nhân hoá là sắp xếp
    /// lại, không phải cắt bớt lựa chọn của người dùng.
    /// </summary>
    private async Task<IReadOnlyList<RecommendedLoungeShowDto>> RankByTasteAsync(
        TasteProfile taste, string? city, int limit, string reasonWhenMatched,
        IReadOnlySet<int> alreadyHas, CancellationToken ct)
    {
        var candidates = await _showRepo.GetTrendingAsync(
            taste.KnowsNothing ? limit : CandidatePoolSize, city, ct);

        // Không biết gì về người hỏi thì trả về đúng bảng đang được quan tâm. Xếp theo một cái gu
        // rỗng chỉ tạo ra thứ tự ngẫu nhiên đội lốt cá nhân hoá.
        candidates = PreferUnseen(candidates, alreadyHas, limit, s => s.Id);

        if (taste.KnowsNothing || candidates.Count == 0)
            return candidates.Take(limit).Select(s => s.ToRecommendedDto(0f, "Đang thịnh hành")).ToList();

        var tagsByShow = (await _showRepo.GetShowTagsAsync(candidates.Select(s => s.Id).ToList(), ct))
            .ToDictionary(t => t.ShowId);

        // Thứ tự thịnh hành được giữ làm mốc phá hoà: hai buổi diễn hợp gu ngang nhau thì buổi đang
        // được quan tâm hơn lên trước.
        var trendingRank = candidates
            .Select((s, index) => (s.Id, index))
            .ToDictionary(x => x.Id, x => x.index);

        var ranked = candidates
            .Select(show =>
            {
                var score = tagsByShow.TryGetValue(show.Id, out var tags)
                    ? TasteMatcher.RankingScore(taste, tags)
                    : 0f;
                return (Show: show, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => trendingRank[x.Show.Id])
            .ToList();

        return CapPerVenue(ranked, limit, x => x.Show.LoungeId)
            .Take(limit)
            .Select(x => x.Show.ToRecommendedDto(
                x.Score,
                x.Score > 0 ? reasonWhenMatched : "Đang thịnh hành"))
            .ToList();
    }

    private async Task<IReadOnlyList<RecommendedLoungeShowDto>> FromCacheAsync(
        IReadOnlyList<AiRecommendation> cached, int limit,
        IReadOnlySet<int> alreadyHas, CancellationToken ct)
    {
        var showIds = cached.Select(r => r.LoungeShowId).ToList();
        var shows = await _showRepo.GetRecommendedByIdsAsync(showIds, ct);
        var recByShowId = cached.ToDictionary(r => r.LoungeShowId);

        var ordered = shows.OrderByDescending(s => recByShowId[s.Id].FinalScore).ToList();

        var afterExclusion = PreferUnseen(ordered, alreadyHas, limit, s => s.Id);

        return CapPerVenue(afterExclusion, limit, s => s.LoungeId)
            .Take(limit)
            .Select(s => s.ToRecommendedDto(recByShowId[s.Id].FinalScore, recByShowId[s.Id].Reason))
            .ToList();
    }
}
