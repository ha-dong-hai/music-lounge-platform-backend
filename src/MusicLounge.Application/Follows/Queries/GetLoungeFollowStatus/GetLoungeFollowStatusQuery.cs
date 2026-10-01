using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Follows.DTOs;

namespace MusicLounge.Application.Follows.Queries.GetLoungeFollowStatus;

public sealed record GetLoungeFollowStatusQuery(IReadOnlyList<int> LoungeIds)
    : IQuery<IReadOnlyList<LoungeFollowStatusDto>>;
