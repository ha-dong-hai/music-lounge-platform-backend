using MediatR;
using MusicLounge.Application.Common;
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

        // MLACP-402: vé bán tại quầy không có người mua, nên trước đây không ai mở lại được — kể cả để in lại khi khách làm mất vé.
        // Nhân viên/chủ của đúng phòng trà giữ vé đó thay khách. Vé có người mua vẫn chỉ người mua xem, để mã QR của khách mua
        // online không lộ ra cho người khác.
        var isBuyer = ticket.BuyerId == _currentUser.UserId;
        var isVenueCounterTicket = ticket.BuyerId is null
            && VenueOperatorAccess.CanOperate(_currentUser, ticket.Show.LoungeId, ticket.Show.Lounge.OwnerId);
        if (!isBuyer && !isVenueCounterTicket)
            throw new ForbiddenException("Bạn không có quyền xem vé này.");

        return new TicketDetailDto(
            ticket.Id,
            ticket.ShowId,
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
                // MLACP-609: suy chỗ ngồi từ KHU của hạng vé, cùng cách màn soát vé đã làm từ MLACP-303 (GetTicketByQr,
                // CheckInTicket). Trước đây riêng trang "vé của tôi" trả thẳng cột SeatInfo — cột chưa từng được ghi —
                // nên khán giả chọn mua vé ở "Khu Giữa" rồi nhận tấm vé in "không xếp chỗ cố định", trong khi nhân viên
                // quét đúng vé đó lại thấy tên khu. Vé bán theo khu (mỗi hạng vé vào cửa gắn một khu, MLACP-589), nên
                // tên khu chính là thông tin chỗ ngồi duy nhất có.
                ticket.PhysicalDetail.SeatInfo ?? ticket.Tier.Zone?.Name,
                ticket.PhysicalDetail.CheckedInAt),
            ticket.LivestreamDetail is null ? null : new TicketLivestreamDetailDto(
                ticket.LivestreamDetail.AccessToken),
            TicketRefundPolicy.FullRefundUntil(ticket.Show, ticket, ticket.Tier.AccessType));
    }
}
