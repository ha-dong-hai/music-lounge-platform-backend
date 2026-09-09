using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetRecommendedLoungeShows;

/// <param name="RecentShowIds">
/// Chỉ dùng cho khách chưa đăng nhập: những buổi diễn họ vừa xem, do phía giao diện tự giữ. Máy chủ
/// suy ra gu từ thẻ phân loại của chúng để sắp xếp câu trả lời, rồi quên đi — không lưu, không gắn
/// với định danh nào. Người đã đăng nhập thì lấy gu từ sở thích họ tự khai, nên bỏ qua trường này.
/// </param>
/// <param name="GenreIds">
/// Cũng chỉ dùng cho khách chưa đăng nhập: thể loại họ vừa chọn trên giao diện.
/// </param>
/// <param name="City">Giới hạn theo thành phố, cho cả hai nhóm.</param>
public sealed record GetRecommendedLoungeShowsQuery(
    int Limit = 10,
    IReadOnlyList<int>? RecentShowIds = null,
    IReadOnlyList<int>? GenreIds = null,
    string? City = null) : IQuery<IReadOnlyList<RecommendedLoungeShowDto>>;
