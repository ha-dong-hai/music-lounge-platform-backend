using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetShowRatings;

/// <param name="Score">MLACP-573: chỉ lấy nhận xét đúng số sao này (1–5). Chỉ lọc DANH SÁCH nhận xét; điểm trung bình,
/// tổng số và phân bố sao vẫn tính trên toàn bộ đánh giá còn hiệu lực — người xem lọc "1 sao" vẫn cần thấy bức tranh chung.</param>
public sealed record GetShowRatingsQuery(Guid ShowId, int Page = 1, int PageSize = 20, int? Score = null)
    : IQuery<ShowRatingsDto>;
