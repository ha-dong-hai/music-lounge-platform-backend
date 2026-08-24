using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

// MLACP-58 + MLACP-59: GenreIds/MoodIds/AtmosphereIds (-58) va Keyword/Format/DateFrom/DateTo
// + phan trang (-59). Cac field con lai cua LoungeShowSearchParams (PerformerId/LoungeId/
// City/District/Ward/MinPrice/MaxPrice) chua co task Jira nao yeu cau expose qua API nay —
// ToSearchParams() truyen null/default cho cac field do, chua bat len query nay.
public sealed record SearchLoungeShowsQuery(
    int[]? GenreIds,
    int[]? MoodIds,
    int[]? AtmosphereIds,
    string? Keyword,
    LoungeShowFormat? Format,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    int Page = 1,
    int PageSize = 10,
    LoungeShowSortBy SortBy = LoungeShowSortBy.Newest)
    : IQuery<PaginatedResult<LoungeShowListItemDto>>;
