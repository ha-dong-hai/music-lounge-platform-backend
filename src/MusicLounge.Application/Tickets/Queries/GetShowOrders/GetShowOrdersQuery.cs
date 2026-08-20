using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Queries.GetShowOrders;

public sealed record GetShowOrdersQuery(int ShowId, int Page = 1, int PageSize = 50)
    : IQuery<PaginatedResult<ShowOrderDto>>;
