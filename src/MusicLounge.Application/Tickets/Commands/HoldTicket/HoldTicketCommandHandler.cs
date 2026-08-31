using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Tickets.Commands.HoldTicket;

internal sealed class HoldTicketCommandHandler : IRequestHandler<HoldTicketCommand, HoldTicketResultDto>
{
    private const decimal LowStockThresholdRatio = 0.10m;

    private readonly IUnitOfWork _uow;
    private readonly ITicketRepository _ticketRepo;
    private readonly IShowBookingLock _bookingLock;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;
    private readonly IBackgroundJobService _backgroundJobs;

    public HoldTicketCommandHandler(
        IUnitOfWork uow, ITicketRepository ticketRepo, IShowBookingLock bookingLock,
        ICurrentUserService currentUser, ISystemConfigService config,
        INotificationService notifications, IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _ticketRepo = ticketRepo;
        _bookingLock = bookingLock;
        _currentUser = currentUser;
        _config = config;
        _notifications = notifications;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<HoldTicketResultDto> Handle(HoldTicketCommand request, CancellationToken ct)
    {
        var priceRepo = _uow.Repository<TicketPrice, int>();
        var price = await priceRepo.GetByIdAsync(request.PriceId, ct)
            ?? throw new NotFoundException(nameof(TicketPrice), request.PriceId);

        var tierRepo = _uow.Repository<TicketTier, int>();
        var tier = await tierRepo.GetByIdAsync(price.TierId, ct)
            ?? throw new NotFoundException(nameof(TicketTier), price.TierId);

        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(tier.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), tier.LoungeShowId);

        if (show.Status is not LoungeShowStatus.Published and not LoungeShowStatus.Ongoing)
            throw new DomainException(
                $"Không thể đặt vé — show hiện ở trạng thái '{show.Status}', chỉ mở bán khi show đã Published hoặc đang diễn ra.");

        ValidateSaleWindow(price);

        int holdId;
        DateTimeOffset holdExpiresAt;

        // Serialize quota-check-then-reserve per show: without this, two buyers can both read
        // "seats available" before either has written their hold, and both pass — overselling
        // the show. See IShowBookingLock for the horizontal-scaling caveat.
        await using (await _bookingLock.AcquireAsync(show.Id, ct))
        {
            await ValidateQuotaAsync(price, tier, show, request.Quantity, ct);

            var holdMinutes = await _config.GetIntAsync(ConfigKeys.TicketHoldMinutes, 15, ct);

            var hold = new TicketHold
            {
                UserId = _currentUser.UserId,
                PriceId = request.PriceId,
                Quantity = request.Quantity,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(holdMinutes),
                CreatedAt = DateTimeOffset.UtcNow
            };

            _uow.Repository<TicketHold, int>().Add(hold);
            await _uow.SaveChangesAsync(ct);

            holdId = hold.Id;
            holdExpiresAt = hold.ExpiresAt;
        }

        // Highest-intent signal in the whole behaviour taxonomy — starting a hold means the buyer
        // is actually trying to purchase, not just browsing. Feeds RecomputeUserEventScoresJob's
        // collaborative-filtering matrix, same as every other BehaviourAction.
        if (_currentUser.IsAuthenticated)
            _backgroundJobs.EnqueueLogUserBehaviour(_currentUser.UserId, show.Id, BehaviourAction.ClickTicket);

        // Deliberately OUTSIDE the lock: the invariant the lock protects (no overselling) is already
        // fully committed by this point. NotifyIfLowStockAsync does a handful of reads plus one
        // synchronous Hangfire enqueue per wishlister — on a popular show crossing the low-stock
        // threshold (i.e. exactly when many buyers are racing for the last seats), that's real I/O
        // with unbounded cardinality that has nothing to do with quota correctness. Holding the
        // per-show semaphore through it previously serialized every OTHER concurrent buyer behind
        // whichever request happened to trip the threshold, turning the show's sellout moment into a
        // self-inflicted bottleneck.
        await NotifyIfLowStockAsync(price, show, request.Quantity, ct);

        return new HoldTicketResultDto(holdId, holdExpiresAt);
    }

    private async Task NotifyIfLowStockAsync(
        TicketPrice price, LoungeShow show, int justHeldQuantity, CancellationToken ct)
    {
        if (!price.Quota.HasValue) return;

        var confirmedCount = await _uow.Repository<Ticket, Guid>().CountAsync(
            t => t.PriceId == price.Id
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Pending), ct);
        var activeHolds = await _uow.Repository<TicketHold, int>().FindAsync(
            h => h.PriceId == price.Id && !h.IsReleased, ct);
        var heldCount = activeHolds.Where(h => h.ExpiresAt > DateTimeOffset.UtcNow).Sum(h => h.Quantity);

        var remaining = price.Quota.Value - confirmedCount - heldCount;
        var threshold = Math.Max(1, (int)(price.Quota.Value * LowStockThresholdRatio));
        if (remaining > threshold) return;

        var wishlisters = await _uow.Repository<ShowWishlist, int>()
            .FindAsync(w => w.LoungeShowId == show.Id, ct);

        foreach (var w in wishlisters)
        {
            await _notifications.NotifyAsync(
                w.UserId,
                NotificationType.WishlistLowStock,
                "Sắp hết vé!",
                $"\"{show.Name}\" trong danh sách yêu thích của bạn chỉ còn {Math.Max(0, remaining)} vé.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);
        }

        if (wishlisters.Count > 0)
            await _uow.SaveChangesAsync(ct);
    }

    private static void ValidateSaleWindow(TicketPrice price)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < price.SaleStart || now > price.SaleEnd)
            throw new DomainException("Đợt bán vé này chưa mở hoặc đã kết thúc.");
    }

    private async Task ValidateQuotaAsync(
        TicketPrice price, TicketTier tier, LoungeShow show,
        int quantity, CancellationToken ct)
    {
        // Kiểm tra quota của đợt bán cụ thể
        if (price.Quota.HasValue)
        {
            var reserved = await _ticketRepo.GetReservedQuantitiesByPriceIdsAsync([price.Id], ct);
            if (reserved[price.Id] + quantity > price.Quota.Value)
                throw new DomainException("Số lượng vé trong đợt bán này không đủ. Vui lòng giảm số lượng hoặc chọn đợt bán/hạng vé khác.");
        }

        // Kiểm tra tổng sức chứa của khu vực (TotalCapacity) — ngăn overselling qua nhiều đợt
        if (tier.TotalCapacity.HasValue)
        {
            var tierReserved = await _ticketRepo.GetReservedQuantityByTierAsync(tier.Id, ct);
            if (tierReserved + quantity > tier.TotalCapacity.Value)
                throw new DomainException("Khu vực ngồi này đã hết chỗ. Vui lòng chọn khu vực hoặc hạng vé khác.");
        }

        // §6.11: SUM(price.quota) của MỌI tier cùng trỏ 1 zone vật lý không được vượt sức chứa
        // thật của zone — check TotalCapacity ở trên chỉ giới hạn từng tier riêng lẻ, không bắt
        // được trường hợp 2 tier khác nhau (vd VIP + Standard) cùng zone cộng lại vượt sức chứa.
        if (tier.ZoneId.HasValue)
        {
            var zone = await _uow.Repository<SeatingZone, int>().GetByIdAsync(tier.ZoneId.Value, ct);
            if (zone is not null)
            {
                var zoneReserved = await _ticketRepo.GetReservedQuantityByZoneAsync(show.Id, tier.ZoneId.Value, ct);
                if (zoneReserved + quantity > zone.Capacity)
                    throw new DomainException("Khu vực này đã vượt sức chứa thực tế. Vui lòng chọn khu vực hoặc hạng vé khác.");
            }
        }

        // Kiểm tra tổng sức chứa của cả show theo loại truy cập
        var accessType = tier.AccessType;
        var relevantQuota = accessType == AccessType.Physical ? show.OfflineQuota : show.OnlineQuota;
        if (relevantQuota.HasValue)
        {
            var showReserved = await _ticketRepo.GetReservedQuantityByShowAndAccessTypeAsync(show.Id, accessType, ct);
            if (showReserved + quantity > relevantQuota.Value)
                throw new DomainException(
                    accessType == AccessType.Physical ? "Show đã hết vé vật lý." : "Show đã hết vé online.");
        }

        // D14: giới hạn tổng vé/event của gói subscription Owner đang Active — check ở
        // CreateTicketTierCommandHandler chỉ áp dụng khi TotalCapacity được set lúc tạo tier, nên
        // 1 tier để trống TotalCapacity (hợp lệ, chỉ dùng Quota) bán được vô hạn định; subscription
        // cũng có thể hết hạn/hạ gói SAU khi tier đã tồn tại. Check lại ở đây — đúng điểm tiền/vé
        // đổi chủ thật — để không thể bị bỏ qua bằng cách để trống 1 field tuỳ chọn.
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        var activeSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
        var activeSub = activeSubs
            .Where(s => s.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(s => s.StartedAt).FirstOrDefault();
        if (activeSub is not null)
        {
            var showReservedTotal = await _ticketRepo.GetReservedQuantityByShowAsync(show.Id, ct);
            if (showReservedTotal + quantity > activeSub.MaxTicketsPerEventSnapshot)
                throw new DomainException(
                    $"Show đã đạt giới hạn {activeSub.MaxTicketsPerEventSnapshot} vé/event của gói subscription hiện tại. " +
                    "Owner của venue có thể nâng cấp gói để tăng giới hạn này (xem GET /subscriptions/packages).");
        }
    }
}
