using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Follows.DTOs;

namespace MusicLounge.Application.Follows.Queries.GetMyFollowedLounges;

public sealed record GetMyFollowedLoungesQuery(int Page, int PageSize)
    : IQuery<PaginatedResult<FollowedLoungeDto>>;
