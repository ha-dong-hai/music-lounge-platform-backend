using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

public sealed record SearchLoungeShowsQuery(
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
    bool IncludeSoldOut = true,
    bool IncludeEnded = false,
    int Page = 1,
    int PageSize = 10,
    LoungeShowSortBy SortBy = LoungeShowSortBy.Newest)
    : IQuery<PaginatedResult<LoungeShowListItemDto>>;
