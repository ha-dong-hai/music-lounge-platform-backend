using MediatR;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Queries.GetTicketByQr;

internal sealed class GetTicketByQrQueryHandler
    : IRequestHandler<GetTicketByQrQuery, TicketDetailDto>
{
    private readonly ITicketRepository _ticketRepo;

    public GetTicketByQrQueryHandler(ITicketRepository ticketRepo)
        => _ticketRepo = ticketRepo;

    public async Task<TicketDetailDto> Handle(GetTicketByQrQuery request, CancellationToken ct)
    {
        var ticket = await _ticketRepo.GetByQrCodeAsync(request.QrCode, ct)
            ?? throw new NotFoundException("Ticket", request.QrCode);

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
