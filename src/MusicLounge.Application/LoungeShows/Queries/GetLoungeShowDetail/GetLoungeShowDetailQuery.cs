using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowDetail;

public sealed record GetLoungeShowDetailQuery(int ShowId)
    : IQuery<LoungeShowDetailDto>;
