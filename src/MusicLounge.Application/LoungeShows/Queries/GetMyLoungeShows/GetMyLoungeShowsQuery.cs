using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Queries.GetMyLoungeShows;

public sealed record GetMyLoungeShowsQuery(
    LoungeShowStatus? Status,
    int Page = 1,
    int PageSize = 10,
    LoungeShowSortBy SortBy = LoungeShowSortBy.Newest
) : IQuery<PaginatedResult<LoungeShowListItemDto>>;
