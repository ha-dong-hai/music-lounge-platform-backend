using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Follows.DTOs;

namespace MusicLounge.Application.Follows.Queries.GetLoungeFollowStatus;

public sealed record GetLoungeFollowStatusQuery(IReadOnlyList<Guid> LoungeIds)
    : IQuery<IReadOnlyList<LoungeFollowStatusDto>>;
