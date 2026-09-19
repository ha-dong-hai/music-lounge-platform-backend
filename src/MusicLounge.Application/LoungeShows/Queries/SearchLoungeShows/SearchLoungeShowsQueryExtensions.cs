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
            City: q.City,
            // MLACP-457: District/Ward van de trong — xem chu thich o SearchLoungeShowsQuery.
            District: null,
            Ward: null,
            DateFrom: q.DateFrom,
            DateTo: q.DateTo,
            Format: q.Format,
            MinPrice: q.MinPrice,
            MaxPrice: q.MaxPrice,
            IncludeSoldOut: q.IncludeSoldOut,
            IncludeEnded: false,
            Page: q.Page,
            PageSize: q.PageSize,
            SortBy: q.SortBy);
}
