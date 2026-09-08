using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowsByLounge;

/// <summary>
/// Các buổi diễn của một phòng trà, cho trang giới thiệu venue mà khán giả xem trước khi mua vé.
/// Khác GetMyLoungeShows — cái đó là danh sách riêng của chủ venue đang đăng nhập, gồm cả bản nháp.
/// </summary>
public sealed record GetLoungeShowsByLoungeQuery(int LoungeId, int Page = 1, int PageSize = 10)
    : IQuery<PaginatedResult<LoungeShowListItemDto>>;
