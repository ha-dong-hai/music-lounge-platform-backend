using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Queries.GetTicketDetail;

internal sealed class GetTicketDetailQueryHandler : IRequestHandler<GetTicketDetailQuery, TicketDetailDto>
{
    private readonly ITicketRepository _ticketRepo;
    private readonly ICurrentUserService _currentUser;

    public GetTicketDetailQueryHandler(ITicketRepository ticketRepo, ICurrentUserService currentUser)
    {
        _ticketRepo = ticketRepo;
        _currentUser = currentUser;
    }

    public async Task<TicketDetailDto> Handle(GetTicketDetailQuery request, CancellationToken ct)
    {
        var ticket = await _ticketRepo.GetByIdWithDetailsAsync(request.TicketId, ct)
            ?? throw new NotFoundException("Ticket", request.TicketId);

        if (ticket.BuyerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xem vé này.");

        return new TicketDetailDto(
            ticket.Id,
            ticket.Show.Name,
            ticket.Show.Lounge.Name,
            ticket.Show.Lounge.Address.FullAddress,
            ticket.Show.ScheduledStart,
            ticket.Show.ScheduledEnd,
            ticket.Tier.Name,
            ticket.Price.Name,
            ticket.Price.Price,
            ticket.Tier.AccessType,
            ticket.Status,
            ticket.QrCode,
            ticket.CreatedAt,
            ticket.PhysicalDetail is null ? null : new PhysicalDetailDto(
                ticket.PhysicalDetail.SeatInfo,
                ticket.PhysicalDetail.CheckedInAt),
            ticket.LivestreamDetail is null ? null : new TicketLivestreamDetailDto(
                ticket.LivestreamDetail.AccessToken));
    }
}
