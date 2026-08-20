using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.SellWalkInTicket;

internal sealed class SellWalkInTicketCommandHandler
    : IRequestHandler<SellWalkInTicketCommand, WalkInSaleResultDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ITicketRepository _ticketRepo;
    private readonly ILoungeShowRepository _showRepo;
    private readonly IShowBookingLock _bookingLock;
    private readonly IPublisher _publisher;

    public SellWalkInTicketCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ITicketRepository ticketRepo,
        ILoungeShowRepository showRepo,
        IShowBookingLock bookingLock,
        IPublisher publisher)
    {
        _uow = uow;
        _currentUser = currentUser;
        _ticketRepo = ticketRepo;
        _showRepo = showRepo;
        _bookingLock = bookingLock;
        _publisher = publisher;
    }

    public async Task<WalkInSaleResultDto> Handle(SellWalkInTicketCommand request, CancellationToken ct)
    {
        var priceRepo = _uow.Repository<TicketPrice, int>();
        var price = await priceRepo.GetByIdAsync(request.PriceId, ct)
            ?? throw new NotFoundException(nameof(TicketPrice), request.PriceId);

        var tierRepo = _uow.Repository<TicketTier, int>();
        var tier = await tierRepo.GetByIdAsync(price.TierId, ct)
            ?? throw new NotFoundException(nameof(TicketTier), price.TierId);

        var showRepoGeneric = _uow.Repository<LoungeShow, int>();
        var show = await showRepoGeneric.GetByIdAsync(tier.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), tier.LoungeShowId);

        var loungeOwnerId = await _showRepo.GetLoungeOwnerIdAsync(tier.LoungeShowId, ct) ?? 0;
        if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, loungeOwnerId))
            throw new ForbiddenException("Bạn không có quyền bán vé tại quầy cho venue này.");

        if (tier.AccessType != AccessType.Physical)
            throw new DomainException("Chỉ có thể bán vé vật lý tại quầy.");

        if (price.PurchaseChannel == PurchaseChannel.Online)
            throw new DomainException("Đợt bán vé này chỉ bán online, không thể bán tại quầy.");

        if (show.Status is not LoungeShowStatus.Published and not LoungeShowStatus.Ongoing)
            throw new DomainException("Không thể bán vé cho show này.");

        var now = DateTimeOffset.UtcNow;
        if (now < price.SaleStart || now > price.SaleEnd)
            throw new DomainException("Đợt bán vé này chưa mở hoặc đã kết thúc.");

        // Serialize quota-check-then-reserve per show — same race as HoldTicketCommandHandler:
        // a Staff walk-in sale and an online buyer's hold can target the same show concurrently.
        await using (await _bookingLock.AcquireAsync(show.Id, ct))
        {
            await ValidateQuotaAsync(price, tier, show, request.Quantity, loungeOwnerId, ct);

            var totalAmount = price.Price * request.Quantity;

            var payment = new Payment
            {
                OrderId = $"WI-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40],
                PayerId = null,
                GrossAmount = totalAmount,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "WalkIn",
                ReferenceId = tier.LoungeShowId.ToString(),
                PaidAt = now,
                CreatedAt = now
            };
            _uow.Repository<Payment, int>().Add(payment);
            await _uow.SaveChangesAsync(ct);

            var tickets = Enumerable.Range(0, request.Quantity).Select(_ => new Ticket
            {
                Id = Guid.NewGuid(),
                BuyerId = null,
                PriceId = price.Id,
                TierId = tier.Id,
                ShowId = tier.LoungeShowId,
                PaymentId = payment.Id,
                Status = TicketStatus.Confirmed,
                QrCode = Guid.NewGuid().ToString("N"),
                PurchaseChannel = PurchaseChannel.Offline,
                CreatedAt = now
            }).ToList();

            _uow.Repository<Ticket, Guid>().AddRange(tickets);
            await _uow.SaveChangesAsync(ct);

            foreach (var ticket in tickets)
                _ticketRepo.AddPhysicalDetail(new PhysicalTicketDetail
                {
                    TicketId = ticket.Id,
                    SoldByStaffId = _currentUser.UserId
                });

            await _uow.SaveChangesAsync(ct);

            await _publisher.Publish(new TicketPaymentConfirmed(
                PaymentId: payment.Id,
                UserId: 0,
                OwnerId: loungeOwnerId,
                TicketIds: tickets.Select(t => t.Id).ToArray(),
                LivestreamId: null), ct);

            return new WalkInSaleResultDto(payment.Id, totalAmount, tickets.Select(t => t.Id).ToArray());
        }
    }

    private async Task ValidateQuotaAsync(
        TicketPrice price, TicketTier tier, LoungeShow show, int quantity, int loungeOwnerId, CancellationToken ct)
    {
        if (price.Quota.HasValue)
        {
            var reserved = await _ticketRepo.GetReservedQuantitiesByPriceIdsAsync([price.Id], ct);
            if (reserved[price.Id] + quantity > price.Quota.Value)
                throw new DomainException("Số lượng vé trong đợt bán này không đủ.");
        }

        if (tier.TotalCapacity.HasValue)
        {
            var tierReserved = await _ticketRepo.GetReservedQuantityByTierAsync(tier.Id, ct);
            if (tierReserved + quantity > tier.TotalCapacity.Value)
                throw new DomainException("Khu vực ngồi này đã hết chỗ.");
        }

        // §6.11: cùng check tổng sức chứa zone như HoldTicketCommandHandler — walk-in cũng phải
        // chịu chung giới hạn vật lý của khu vực, không chỉ đường mua online mới bị chặn.
        if (tier.ZoneId.HasValue)
        {
            var zone = await _uow.Repository<SeatingZone, int>().GetByIdAsync(tier.ZoneId.Value, ct);
            if (zone is not null)
            {
                var zoneReserved = await _ticketRepo.GetReservedQuantityByZoneAsync(show.Id, tier.ZoneId.Value, ct);
                if (zoneReserved + quantity > zone.Capacity)
                    throw new DomainException("Khu vực này đã vượt sức chứa thực tế.");
            }
        }

        if (show.OfflineQuota.HasValue)
        {
            var showReserved = await _ticketRepo.GetReservedQuantityByShowAndAccessTypeAsync(
                show.Id, AccessType.Physical, ct);
            if (showReserved + quantity > show.OfflineQuota.Value)
                throw new DomainException("Show đã hết vé vật lý.");
        }

        // D14: cùng giới hạn subscription như HoldTicketCommandHandler — walk-in cũng đổi vé/tiền
        // thật nên phải chịu cùng giới hạn, không chỉ đường mua online mới bị chặn.
        var activeSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == loungeOwnerId && s.Status == SubscriptionStatus.Active, ct);
        var activeSub = activeSubs
            .Where(s => s.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(s => s.StartedAt).FirstOrDefault();
        if (activeSub is not null)
        {
            var showReservedTotal = await _ticketRepo.GetReservedQuantityByShowAsync(show.Id, ct);
            if (showReservedTotal + quantity > activeSub.MaxTicketsPerEventSnapshot)
                throw new DomainException(
                    $"Show đã đạt giới hạn {activeSub.MaxTicketsPerEventSnapshot} vé/event của gói subscription hiện tại.");
        }
    }
}
