using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

// MLACP-58 + MLACP-59: GenreIds/MoodIds/AtmosphereIds (-58) va Keyword/Format/DateFrom/DateTo
// + phan trang (-59).
//
// MLACP-457: mo them City/MinPrice/MaxPrice/IncludeSoldOut. Truoc do frontend van gui 5 tham so
// (city, district, minPrice, maxPrice, includeSoldOut) va ASP.NET BO QUA IM LANG tham so la — nguoi
// dung chon bo loc xong thay ket qua khong doi, khong loi, khong bao gi. Tang repository da loc duoc
// ca 5 tu lau, chi thieu cho noi len query nay.
//
// KHONG mo District: cap huyen da bi bai bo tu 01/07/2025 (Luat To chuc chinh quyen dia phuong so
// 72/2025/QH15, Dieu 4 — chinh quyen 2 cap tinh + xa), va du lieu quan cua moi phong tra dang hoat
// dong tren Azure deu rong. Chu du an da chot bo o loc quan ben frontend. Ward/PerformerId/LoungeId
// van chua co task nao yeu cau expose.
public sealed record SearchLoungeShowsQuery(
    int[]? GenreIds,
    int[]? MoodIds,
    int[]? AtmosphereIds,
    string? Keyword,
    LoungeShowFormat? Format,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? City = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool IncludeSoldOut = true,
    int Page = 1,
    int PageSize = 10,
    LoungeShowSortBy SortBy = LoungeShowSortBy.Newest)
    : IQuery<PaginatedResult<LoungeShowListItemDto>>;
