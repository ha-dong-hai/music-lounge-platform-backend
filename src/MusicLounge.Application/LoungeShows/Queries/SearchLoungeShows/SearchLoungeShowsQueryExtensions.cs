using MusicLounge.Application.Common.Interfaces.Repositories;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

internal static class SearchLoungeShowsQueryExtensions
{
    internal static LoungeShowSearchParams ToSearchParams(this SearchLoungeShowsQuery q)
        => new(
            Keyword: null,
            GenreIds: q.GenreIds,
            MoodIds: q.MoodIds,
            AtmosphereIds: q.AtmosphereIds,
            PerformerId: null,
            LoungeId: null,
            City: null,
            District: null,
            Ward: null,
            DateFrom: null,
            DateTo: null,
            Format: null,
            MinPrice: null,
            MaxPrice: null,
            IncludeSoldOut: true,
            IncludeEnded: false,
            Page: q.Page,
            PageSize: q.PageSize,
            SortBy: q.SortBy);
}
