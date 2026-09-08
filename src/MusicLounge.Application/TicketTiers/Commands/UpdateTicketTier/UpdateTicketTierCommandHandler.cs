using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.TicketTiers.Commands.UpdateTicketTier;

internal sealed class UpdateTicketTierCommandHandler : IRequestHandler<UpdateTicketTierCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAsyncKeyedLock _lock;
    private readonly ISystemConfigService _config;

    public UpdateTicketTierCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IAsyncKeyedLock @lock,
        ISystemConfigService config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _lock = @lock;
    }

    public async Task<Unit> Handle(UpdateTicketTierCommand request, CancellationToken ct)
    {
        var tierRepo = _uow.Repository<TicketTier, int>();
        var tier = await tierRepo.GetByIdAsync(request.TierId, ct)
            ?? throw new NotFoundException(nameof(TicketTier), request.TierId);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(tier.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), tier.LoungeShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền sửa hạng vé này.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể sửa hạng vé khi event còn ở trạng thái Draft.");

        // D14, kept in sync with CreateTicketTierCommandHandler — that one enforces the
        // MaxTicketsPerEventSnapshot cap when a tier is first created, but Update set TotalCapacity
        // directly with no re-check, letting an Owner raise an already-valid tier's capacity past
        // the subscription's limit after the fact.
        //
        // MLACP-271: the read-other-tiers-then-compare-then-write sequence below is a classic
        // check-then-act race — 2 concurrent Updates on different tiers of the same show (or a
        // Create racing an Update) can each read the total before the other's write lands, both
        // pass the check, and both commit, letting the combined total exceed the subscription cap
        // even though each individual request was valid at the moment it checked. Locked by ShowId
        // (not TierId) because the invariant being protected spans ALL tiers of the show — the same
        // key CreateTicketTierCommandHandler uses, so a Create and an Update can never race each
        // other either.
        await using var _ = await _lock.AcquireAsync($"ticket-tier-capacity:{show.Id}", ct);

        if (request.TotalCapacity.HasValue)
        {
            var activeStatusSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
                s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
            var freeTierCap = await _config.GetIntAsync(
                ConfigKeys.FreeTierMaxTicketsPerEvent,
                SubscriptionEntitlements.DefaultFreeTierMaxTicketsPerEvent, ct);
            var cap = SubscriptionEntitlements.ResolveTicketCap(
                SubscriptionEntitlements.ActivePlan(activeStatusSubs, DateTimeOffset.UtcNow), freeTierCap);

            var otherTiers = await _uow.Repository<TicketTier, int>()
                .FindAsync(t => t.LoungeShowId == show.Id && t.Id != tier.Id, ct);
            var totalCapacity = otherTiers.Sum(t => t.TotalCapacity ?? 0) + request.TotalCapacity.Value;

            if (totalCapacity > cap.MaxTicketsPerEvent)
                throw new DomainException(
                    cap.ExceededMessage($"Tổng sức chứa các hạng vé của buổi hòa nhạc này ({totalCapacity})"));
        }

        tier.Name = request.Name;
        tier.Description = request.Description;
        tier.TotalCapacity = request.TotalCapacity;

        tierRepo.Update(tier);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
