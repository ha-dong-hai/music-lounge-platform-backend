using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

// MLACP-58: chi loc theo GenreIds/MoodIds/AtmosphereIds. Format/DateFrom/DateTo/Keyword
// (MLACP-59) va cac field con lai cua LoungeShowSearchParams (Keyword/PerformerId/LoungeId/
// City/District/Ward/MinPrice/MaxPrice) chua co task Jira nao yeu cau expose qua API nay —
// ToSearchParams() truyen null/default cho cac field do, chua bat len query nay.
public sealed record SearchLoungeShowsQuery(
    int[]? GenreIds,
    int[]? MoodIds,
    int[]? AtmosphereIds,
    int Page = 1,
    int PageSize = 10,
    LoungeShowSortBy SortBy = LoungeShowSortBy.Newest)
    : IQuery<PaginatedResult<LoungeShowListItemDto>>;
