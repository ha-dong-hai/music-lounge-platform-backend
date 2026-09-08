using MusicLounge.Application.Common.Interfaces.Repositories;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

internal static class SearchLoungeShowsQueryExtensions
{
    internal static LoungeShowSearchParams ToSearchParams(this SearchLoungeShowsQuery q)
        => new(
            Keyword: q.Keyword,
            GenreIds: q.GenreIds,
            MoodIds: q.MoodIds,
            AtmosphereIds: q.AtmosphereIds,
            PerformerId: null,
            LoungeId: null,
            City: null,
            District: null,
            Ward: null,
            DateFrom: q.DateFrom,
            DateTo: q.DateTo,
            Format: q.Format,
            MinPrice: null,
            MaxPrice: null,
            IncludeSoldOut: true,
            IncludeEnded: false,
            Page: q.Page,
            PageSize: q.PageSize,
            SortBy: q.SortBy);
}
