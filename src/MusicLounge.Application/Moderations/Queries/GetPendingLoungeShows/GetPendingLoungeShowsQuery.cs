using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.DTOs;

namespace MusicLounge.Application.Moderations.Queries.GetPendingLoungeShows;

public sealed record GetPendingLoungeShowsQuery(
    int Page = 1,
    int PageSize = 20
) : IQuery<PaginatedResult<PendingLoungeShowDto>>;
