using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Queries.GetIncomingTicketTransfers;

internal sealed class GetIncomingTicketTransfersQueryHandler
    : IRequestHandler<GetIncomingTicketTransfersQuery, IReadOnlyList<IncomingTicketTransferDto>>
{
    private readonly ITicketRepository _ticketRepo;
    private readonly ICurrentUserService _currentUser;

    public GetIncomingTicketTransfersQueryHandler(ITicketRepository ticketRepo, ICurrentUserService currentUser)
    {
        _ticketRepo = ticketRepo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<IncomingTicketTransferDto>> Handle(
        GetIncomingTicketTransfersQuery request, CancellationToken ct)
    {
        var tickets = await _ticketRepo.GetIncomingTransfersAsync(_currentUser.UserId, ct);

        return tickets.Select(t => new IncomingTicketTransferDto(
            t.Id,
            t.Show.Name,
            t.Show.Lounge.Name,
            t.Show.ScheduledStart,
            t.Tier.Name,
            t.Price.Name,
            t.Price.Price,
            t.PendingTransferInitiatedAt ?? t.CreatedAt)).ToList();
    }
}
