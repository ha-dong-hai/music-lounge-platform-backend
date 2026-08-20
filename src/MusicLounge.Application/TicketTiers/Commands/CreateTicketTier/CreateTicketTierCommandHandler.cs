using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.TicketTiers.Commands.CreateTicketTier;

internal sealed class CreateTicketTierCommandHandler : IRequestHandler<CreateTicketTierCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateTicketTierCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateTicketTierCommand request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền thiết lập giá vé cho event này.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể thêm hạng vé khi event còn ở trạng thái Draft.");

        // D14: tong TotalCapacity cac tier cua show khong duoc vuot MaxTicketsPerEvent cua goi
        // subscription dang Active (snapshot tai luc dang ky, khong bi anh huong neu gia goi doi sau).
        if (request.TotalCapacity.HasValue)
        {
            var activeStatusSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
                s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
            var activeSub = activeStatusSubs
                .Where(s => s.ExpiresAt > DateTimeOffset.UtcNow)
                .OrderByDescending(s => s.StartedAt).FirstOrDefault();

            if (activeSub is not null)
            {
                var existingTiers = await _uow.Repository<TicketTier, int>()
                    .FindAsync(t => t.LoungeShowId == request.ShowId, ct);
                var totalCapacity = existingTiers.Sum(t => t.TotalCapacity ?? 0) + request.TotalCapacity.Value;

                if (totalCapacity > activeSub.MaxTicketsPerEventSnapshot)
                    throw new DomainException(
                        $"Tổng số vé cho event này ({totalCapacity}) vượt quá giới hạn " +
                        $"{activeSub.MaxTicketsPerEventSnapshot} vé/event của gói subscription hiện tại.");
            }
        }

        var accessType = Enum.Parse<AccessType>(request.AccessType, ignoreCase: true);

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
                SaleEnd = priceInput.SaleEnd
            });
        }

        await _uow.SaveChangesAsync(ct);

        return tier.Id;
    }
}
