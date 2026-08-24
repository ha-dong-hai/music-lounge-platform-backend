using MediatR;
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

    public UpdateTicketTierCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
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

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền sửa hạng vé này.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể sửa hạng vé khi event còn ở trạng thái Draft.");

        // D14, kept in sync with CreateTicketTierCommandHandler — that one enforces the
        // MaxTicketsPerEventSnapshot cap when a tier is first created, but Update set TotalCapacity
        // directly with no re-check, letting an Owner raise an already-valid tier's capacity past
        // the subscription's limit after the fact.
        if (request.TotalCapacity.HasValue)
        {
            var activeStatusSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
                s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
            var activeSub = activeStatusSubs
                .Where(s => s.ExpiresAt > DateTimeOffset.UtcNow)
                .OrderByDescending(s => s.StartedAt).FirstOrDefault();

            if (activeSub is not null)
            {
                var otherTiers = await _uow.Repository<TicketTier, int>()
                    .FindAsync(t => t.LoungeShowId == show.Id && t.Id != tier.Id, ct);
                var totalCapacity = otherTiers.Sum(t => t.TotalCapacity ?? 0) + request.TotalCapacity.Value;

                if (totalCapacity > activeSub.MaxTicketsPerEventSnapshot)
                    throw new DomainException(
                        $"Tổng số vé cho event này ({totalCapacity}) vượt quá giới hạn " +
                        $"{activeSub.MaxTicketsPerEventSnapshot} vé/event của gói subscription hiện tại.");
            }
        }

        tier.Name = request.Name;
        tier.Description = request.Description;
        tier.TotalCapacity = request.TotalCapacity;

        tierRepo.Update(tier);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
