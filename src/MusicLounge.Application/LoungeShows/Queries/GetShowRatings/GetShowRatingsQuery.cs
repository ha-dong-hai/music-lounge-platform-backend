using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetShowRatings;

public sealed record GetShowRatingsQuery(int ShowId, int Page = 1, int PageSize = 20)
    : IQuery<ShowRatingsDto>;
