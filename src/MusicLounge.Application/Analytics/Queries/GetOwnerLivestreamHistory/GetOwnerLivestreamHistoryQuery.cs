using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerLivestreamHistory;

public sealed record GetOwnerLivestreamHistoryQuery(
    int LoungeId,
    int Page = 1,
    int PageSize = 10
) : IQuery<PaginatedResult<LivestreamHistoryItemDto>>;
