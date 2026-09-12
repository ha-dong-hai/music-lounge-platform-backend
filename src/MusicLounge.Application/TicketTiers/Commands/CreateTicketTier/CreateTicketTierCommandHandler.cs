using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.TicketTiers.Commands.CreateTicketTier;

internal sealed class CreateTicketTierCommandHandler : IRequestHandler<CreateTicketTierCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAsyncKeyedLock _lock;
    private readonly ISystemConfigService _config;
    private readonly ILivestreamRepository _livestreamRepo;

    public CreateTicketTierCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IAsyncKeyedLock @lock,
        ISystemConfigService config, ILivestreamRepository livestreamRepo)
    {
        _livestreamRepo = livestreamRepo;
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _lock = @lock;
    }

    public async Task<int> Handle(CreateTicketTierCommand request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền thiết lập giá vé cho event này.");

        var accessType = Enum.Parse<AccessType>(request.AccessType, ignoreCase: true);

        // MLACP-388: buoi dien da dang ma phai chuyen sang online (MLACP-383 hoan 100% ve vao cua) truoc day khong the co
        // hang ve livestream — tao hang ve chi chay khi Draft va la duong duy nhat — nen khong ban duoc ve xem online. Nay
        // duoc them DUY NHAT hang ve livestream, cho buoi dien Online/Hybrid da co livestream; gia chua mo ban cho toi khi
        // Admin duyet (ReviewTicketTier). Hang ve vao cua va gia da cong bo van khoa nhu da hua voi nguoi mua.
        var addingAfterPublish = show.Status is LoungeShowStatus.Published or LoungeShowStatus.Ongoing;
        if (show.Status != LoungeShowStatus.Draft && !addingAfterPublish)
            throw new DomainException("Chỉ có thể thêm hạng vé khi event còn ở trạng thái Draft.");

        if (addingAfterPublish)
        {
            if (accessType != AccessType.Livestream || show.Format == LoungeShowFormat.Offline)
                throw new DomainException(
                    "Buổi diễn đã đăng chỉ được thêm hạng vé livestream, và chỉ khi hình thức là online hoặc hybrid — " +
                    "hạng vé vào cửa và giá đã công bố được giữ nguyên như đã hứa với người mua.");
            if (await _livestreamRepo.GetByShowIdAsync(show.Id, ct) is null)
                throw new DomainException("Cần thiết lập livestream cho buổi diễn trước khi thêm hạng vé livestream.");
        }

        // D14: tong TotalCapacity cac tier cua show khong duoc vuot MaxTicketsPerEvent cua goi
        // subscription dang Active (snapshot tai luc dang ky, khong bi anh huong neu gia goi doi sau).
        //
        // MLACP-271: locked by ShowId (same key UpdateTicketTierCommandHandler uses) so this
        // read-existing-tiers-then-compare-then-write can't race a concurrent Update (or another
        // concurrent Create) on the same show — see that handler's comment for the full race
        // scenario this closes.
        await using var _ = await _lock.AcquireAsync($"ticket-tier-capacity:{request.ShowId}", ct);

        if (request.TotalCapacity.HasValue)
        {
            var activeStatusSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
                s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
            // Cap luon ton tai — goi dang hoat dong, hoac muc mien phi. Day la thoi diem venue TAO
            // MOT CAM KET MOI, dung cho gioi han nen can.
            var freeTierCap = await _config.GetIntAsync(
                ConfigKeys.FreeTierMaxTicketsPerEvent,
                SubscriptionEntitlements.DefaultFreeTierMaxTicketsPerEvent, ct);
            var cap = SubscriptionEntitlements.ResolveTicketCap(
                SubscriptionEntitlements.ActivePlan(activeStatusSubs, DateTimeOffset.UtcNow), freeTierCap);

            var existingTiers = await _uow.Repository<TicketTier, int>()
                .FindAsync(t => t.LoungeShowId == request.ShowId, ct);
            var totalCapacity = existingTiers.Sum(t => t.TotalCapacity ?? 0) + request.TotalCapacity.Value;

            if (totalCapacity > cap.MaxTicketsPerEvent)
                throw new DomainException(
                    cap.ExceededMessage($"Tổng sức chứa các hạng vé của buổi hòa nhạc này ({totalCapacity})"));
        }

        var tier = new TicketTier
        {
            LoungeShowId = request.ShowId,
            Name = request.Name,
            Description = request.Description,
            AccessType = accessType,
            ZoneId = accessType == AccessType.Physical ? request.ZoneId : null,
            TotalCapacity = request.TotalCapacity
        };

        _uow.Repository<TicketTier, int>().Add(tier);
        await _uow.SaveChangesAsync(ct);

        foreach (var priceInput in request.Prices)
        {
            var channel = Enum.Parse<PurchaseChannel>(priceInput.PurchaseChannel, ignoreCase: true);
            _uow.Repository<TicketPrice, int>().Add(new TicketPrice
            {
                TierId = tier.Id,
                Name = priceInput.Name,
                Price = priceInput.Price,
                Quota = priceInput.Quota,
                PurchaseChannel = channel,
                SaleStart = priceInput.SaleStart,
                SaleEnd = priceInput.SaleEnd,
                // MLACP-388: gia them sau khi dang chua ai duyet — chi mo ban khi Admin dong y.
                IsActive = !addingAfterPublish
            });
        }

        await _uow.SaveChangesAsync(ct);

        if (addingAfterPublish)
        {
            // Cung SLA voi duyet buoi dien va livestream (ND 147/2024 — doc tu system_config, khong co dinh).
            var slaHours = await _config.GetIntAsync(ConfigKeys.ModerationSlaHours, 24, ct);
            _uow.Repository<EventModeration, int>().Add(new EventModeration
            {
                TargetType = ModerationTargetType.TicketTier,
                TargetId = tier.Id,
                SlaDeadline = DateTimeOffset.UtcNow.AddHours(slaHours)
            });
            await _uow.SaveChangesAsync(ct);
        }

        return tier.Id;
    }
}
