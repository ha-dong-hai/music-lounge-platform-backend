using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetSimilarLoungeShows;

public sealed record GetSimilarLoungeShowsQuery(int ShowId) : IQuery<IReadOnlyList<LoungeShowListItemDto>>;
