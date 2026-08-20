using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Queries.GetPublishedLoungeShows;

public sealed record GetPublishedLoungeShowsQuery(
    int Page = 1,
    int PageSize = 10,
    LoungeShowSortBy SortBy = LoungeShowSortBy.Newest,
    bool IncludeSoldOut = true,
    bool Mine = false)
    : IQuery<PaginatedResult<LoungeShowListItemDto>>;
