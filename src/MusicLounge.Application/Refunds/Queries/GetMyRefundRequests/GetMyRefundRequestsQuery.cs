using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Refunds.DTOs;

namespace MusicLounge.Application.Refunds.Queries.GetMyRefundRequests;

public sealed record GetMyRefundRequestsQuery(int Page, int PageSize)
    : IQuery<PaginatedResult<RefundRequestDto>>;
