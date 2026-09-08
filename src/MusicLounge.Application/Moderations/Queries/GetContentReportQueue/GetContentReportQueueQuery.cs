using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.DTOs;

namespace MusicLounge.Application.Moderations.Queries.GetContentReportQueue;

public sealed record GetContentReportQueueQuery(
    int Page = 1,
    int PageSize = 20
) : IQuery<PaginatedResult<ContentReportQueueItemDto>>;
