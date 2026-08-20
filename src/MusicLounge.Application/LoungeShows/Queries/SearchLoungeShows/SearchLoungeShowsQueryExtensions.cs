using MusicLounge.Application.Common.Interfaces.Repositories;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

internal static class SearchLoungeShowsQueryExtensions
{
    internal static LoungeShowSearchParams ToSearchParams(this SearchLoungeShowsQuery q)
        => new(q.Keyword, q.GenreIds, q.MoodIds, q.AtmosphereIds,
               q.PerformerId, q.LoungeId, q.City, q.District, q.Ward,
               q.DateFrom, q.DateTo, q.Format,
               q.MinPrice, q.MaxPrice, q.IncludeSoldOut, q.IncludeEnded,
               q.Page, q.PageSize, q.SortBy);
}
