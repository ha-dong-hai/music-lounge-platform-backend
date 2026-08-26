using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Queries.GetTicketByQr;

// Chi buyer cua chinh ve nay hoac Owner/Staff/Admin cua dung venue (VenueOperatorAccess, giong
// CheckInTicket) moi xem duoc — tra ve day du buyer/gia/AccessToken livestream, khong the mo cho
// bat ky ai co chuoi QrCode.
internal sealed class GetTicketByQrQueryHandler
    : IRequestHandler<GetTicketByQrQuery, TicketDetailDto>
{
    private readonly ITicketRepository _ticketRepo;
    private readonly ICurrentUserService _currentUser;

    public GetTicketByQrQueryHandler(ITicketRepository ticketRepo, ICurrentUserService currentUser)
    {
        _ticketRepo = ticketRepo;
        _currentUser = currentUser;
    }

    public async Task<TicketDetailDto> Handle(GetTicketByQrQuery request, CancellationToken ct)
    {
        var ticket = await _ticketRepo.GetByQrCodeAsync(request.QrCode, ct)
            ?? throw new NotFoundException("Ticket", request.QrCode);

        if (ticket.BuyerId != _currentUser.UserId
            && !VenueOperatorAccess.CanOperate(_currentUser, ticket.Show.LoungeId, ticket.Show.Lounge.OwnerId))
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
