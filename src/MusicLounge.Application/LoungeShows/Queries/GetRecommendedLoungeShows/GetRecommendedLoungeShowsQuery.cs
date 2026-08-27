using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetRecommendedLoungeShows;

public sealed record GetRecommendedLoungeShowsQuery(int Limit = 10)
    : IQuery<IReadOnlyList<RecommendedLoungeShowDto>>;
