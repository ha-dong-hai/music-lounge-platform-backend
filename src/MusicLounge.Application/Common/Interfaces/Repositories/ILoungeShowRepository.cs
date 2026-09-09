using MusicLounge.Application.Analytics.Common;
using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface ILoungeShowRepository : IRepository<LoungeShow, int>
{
    Task<LoungeShow?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);

    Task<PaginatedResult<LoungeShow>> GetPublishedAsync(
        int page, int pageSize, LoungeShowSortBy sortBy,
        bool includeSoldOut, CancellationToken ct = default);

    /// <summary>Shows belonging to the given owner's lounges. Any status (including Draft) when
    /// <paramref name="status"/> is null; otherwise only that status.</summary>
    Task<PaginatedResult<LoungeShow>> GetMineAsync(
        int ownerId, int page, int pageSize, LoungeShowSortBy sortBy,
        LoungeShowStatus? status = null, CancellationToken ct = default);

    Task<PaginatedResult<LoungeShow>> SearchAsync(
        LoungeShowSearchParams searchParams, CancellationToken ct = default);

    Task<PaginatedResult<LoungeShow>> GetByPerformerAsync(
        int performerId, bool includeEnded,
        int page, int pageSize, CancellationToken ct = default);

    Task<PaginatedResult<LoungeShow>> GetByLoungeAsync(
        int loungeId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Thẻ phân loại (thể loại/tâm trạng/không gian) của một tập buổi diễn, đủ để so với gu người
    /// nghe. Truy vấn riêng vì WithDetails() cố ý không nạp tâm trạng và không gian — các danh sách
    /// thông thường không dùng tới chúng, và nạp kèm sẽ bắt mọi endpoint danh sách gánh thêm join.
    /// </summary>
    Task<IReadOnlyList<ShowTags>> GetShowTagsAsync(
        IReadOnlyCollection<int> showIds, CancellationToken ct = default);

    /// <summary>
    /// Buổi diễn mới được đăng gần đây nhất, bất kể đã có ai quan tâm hay chưa.
    ///
    /// Cần riêng vì bảng "đang được quan tâm" xếp theo tương tác, mà buổi diễn vừa đăng thì chưa có
    /// tương tác nào — và khi hoà điểm 0 thì thứ tự là "sắp diễn trước", trong khi buổi mới đăng
    /// bắt buộc cách ngày diễn tối thiểu 7 ngày làm việc nên luôn nằm xa. Kết quả là buổi diễn mới
    /// bị đẩy xuống cuối một cách hệ thống và có thể không bao giờ được ai nhìn thấy.
    /// </summary>
    Task<IReadOnlyList<LoungeShow>> GetRecentlyPublishedAsync(
        int limit, string? city, CancellationToken ct = default);

    Task<IReadOnlyList<LoungeShow>> GetTrendingAsync(
        int limit, string? city, CancellationToken ct = default);

    Task<IReadOnlyList<LoungeShowSuggestionItem>> GetSuggestionsAsync(
        string keyword, int limit, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetDistinctCitiesAsync(CancellationToken ct = default);

    /// <summary>Single JOIN query: returns OwnerId of the lounge hosting the given show, or null if not found.</summary>
    Task<int?> GetLoungeOwnerIdAsync(int showId, CancellationToken ct = default);

    Task<IReadOnlySet<int>> GetWishlistedShowIdsAsync(
        int userId, CancellationToken ct = default);

    Task<PaginatedResult<LoungeShow>> GetWishlistByUserAsync(
        int userId, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<LoungeShow>> GetRecommendedByIdsAsync(
        IReadOnlyList<int> showIds, CancellationToken ct = default);

    /// <summary>Shows "tương tự" trang chi tiết (MLACP-134): cùng phòng trà HOẶC chung ít nhất 1 thể
    /// loại nhạc với <paramref name="showId"/>, loại trừ chính show đó, chỉ Published/Ongoing. Ưu
    /// tiên show khớp CẢ hai tiêu chí trước, còn lại theo ngày diễn gần nhất.</summary>
    Task<IReadOnlyList<LoungeShow>> GetSimilarAsync(
        int showId, int loungeId, IReadOnlyList<int> genreIds, int limit, CancellationToken ct = default);

    /// <summary>
    /// Returns sold (Confirmed+Pending) ticket count plus active hold quantity per priceId.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetSoldAndHeldCountsByPriceAsync(
        IReadOnlyList<int> priceIds, CancellationToken ct = default);
}

public sealed record LoungeShowSearchParams(
    string? Keyword,
    int[]? GenreIds,
    int[]? MoodIds,
    int[]? AtmosphereIds,
    int? PerformerId,
    int? LoungeId,
    string? City,
    string? District,
    string? Ward,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    LoungeShowFormat? Format,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool IncludeSoldOut,
    bool IncludeEnded,
    int Page,
    int PageSize,
    LoungeShowSortBy SortBy);

public sealed record LoungeShowSuggestionItem(int Id, string Name, string? CoverImageUrl);
