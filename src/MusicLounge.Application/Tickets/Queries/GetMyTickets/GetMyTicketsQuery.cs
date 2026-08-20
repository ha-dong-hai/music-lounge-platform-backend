using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Queries.GetMyTickets;

public sealed record GetMyTicketsQuery(int Page = 1, int PageSize = 10)
    : IQuery<PaginatedResult<TicketListItemDto>>;
