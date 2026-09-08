using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.CheckInTicket;

internal sealed class CheckInTicketCommandHandler : IRequestHandler<CheckInTicketCommand, TicketDetailDto>
{
    private readonly ITicketRepository _ticketRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly IAsyncKeyedLock _lock;

    public CheckInTicketCommandHandler(
        ITicketRepository ticketRepo,
        ICurrentUserService currentUser,
        IUnitOfWork uow,
        IAsyncKeyedLock @lock)
    {
        _ticketRepo = ticketRepo;
        _currentUser = currentUser;
        _uow = uow;
        _lock = @lock;
    }

    public async Task<TicketDetailDto> Handle(CheckInTicketCommand request, CancellationToken ct)
    {
        // Same QR scanned at 2 doors within the same instant is the exact failure this guards
        // against: without a lock, both requests can read PhysicalDetail.CheckedInAt == null
        // before either commits, and both succeed — a real double-entry with 1 ticket.
        await using var _ = await _lock.AcquireAsync($"checkin:{request.QrCode}", ct);

        var ticket = await _ticketRepo.GetByQrCodeTrackedAsync(request.QrCode, ct)
            ?? throw new NotFoundException("Ticket", request.QrCode);

        if (!VenueOperatorAccess.CanOperate(_currentUser, ticket.Show.LoungeId, ticket.Show.Lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền check-in vé cho buổi diễn này.");

        if (ticket.Show.Status != LoungeShowStatus.Ongoing)
            throw new DomainException("Chỉ có thể check-in khi buổi diễn đang diễn ra.");

        if (ticket.Tier.AccessType != AccessType.Physical)
            throw new DomainException("Vé online không cần check-in tại cửa.");

        if (ticket.Status != TicketStatus.Confirmed)
            throw new DomainException("Vé không hợp lệ để check-in. Chỉ vé đã xác nhận mới được quét.");

        if (ticket.PhysicalDetail?.CheckedInAt is not null)
            throw new ConflictException("Vé này đã được check-in trước đó.");

        if (ticket.PendingTransferToUserId is not null)
            throw new DomainException("Vé đang trong quá trình chuyển nhượng, không thể check-in lúc này.");

        ticket.Status = TicketStatus.Used;

        if (ticket.PhysicalDetail is null)
        {
            ticket.PhysicalDetail = new PhysicalTicketDetail
            {
                TicketId = ticket.Id,
                CheckedInAt = DateTimeOffset.UtcNow,
                CheckedInByStaffId = _currentUser.UserId
            };
        }
        else
        {
            ticket.PhysicalDetail.CheckedInAt = DateTimeOffset.UtcNow;
            ticket.PhysicalDetail.CheckedInByStaffId = _currentUser.UserId;
        }

        await _uow.SaveChangesAsync(ct);

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
