using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.FnbOrders.DTOs;

namespace MusicLounge.Application.FnbOrders.Queries.GetFnbOrders;

public sealed record GetFnbOrdersQuery(
    int LoungeId,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PaginatedResult<FnbOrderDto>>;
