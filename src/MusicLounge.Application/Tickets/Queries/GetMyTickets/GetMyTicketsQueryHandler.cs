using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Queries.GetMyTickets;

internal sealed class GetMyTicketsQueryHandler
    : IRequestHandler<GetMyTicketsQuery, PaginatedResult<TicketListItemDto>>
{
    private readonly ITicketRepository _ticketRepo;
    private readonly ICurrentUserService _currentUser;

    public GetMyTicketsQueryHandler(ITicketRepository ticketRepo, ICurrentUserService currentUser)
    {
        _ticketRepo = ticketRepo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<TicketListItemDto>> Handle(
        GetMyTicketsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await _ticketRepo.GetByBuyerAsync(
            _currentUser.UserId, page, pageSize, ct);

        return result.Map(t => new TicketListItemDto(
            t.Id,
            t.ShowId,
            t.Show.Name,
            t.Show.Lounge.Name,
            t.Show.Lounge.Address.City,
            t.Show.ScheduledStart,
            t.Tier.Name,
            t.Price.Name,
            t.Price.Price,
            t.Tier.AccessType,
            t.Status,
            t.QrCode,
            t.CreatedAt,
            t.PendingTransferToUserId is not null));
    }
}
