using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetTrendingLoungeShows;

public sealed record GetTrendingLoungeShowsQuery(int Limit = 10, string? City = null)
    : IQuery<IReadOnlyList<LoungeShowListItemDto>>;
