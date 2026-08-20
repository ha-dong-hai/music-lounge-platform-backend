using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Refunds.DTOs;

namespace MusicLounge.Application.Refunds.Queries.GetPendingRefundRequests;

public sealed record GetPendingRefundRequestsQuery(int Page, int PageSize)
    : IQuery<PaginatedResult<RefundRequestDto>>;
