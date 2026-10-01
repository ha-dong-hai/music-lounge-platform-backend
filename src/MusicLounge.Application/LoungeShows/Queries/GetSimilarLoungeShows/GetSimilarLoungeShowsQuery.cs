using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetSimilarLoungeShows;

public sealed record GetSimilarLoungeShowsQuery(Guid ShowId) : IQuery<IReadOnlyList<LoungeShowListItemDto>>;
