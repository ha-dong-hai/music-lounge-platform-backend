using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowsByPerformer;

public sealed record GetLoungeShowsByPerformerQuery(
    int PerformerId,
    bool IncludeEnded = false,
    int Page = 1,
    int PageSize = 10)
    : IQuery<PerformerDetailDto>;
