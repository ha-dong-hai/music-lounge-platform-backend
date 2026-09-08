using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Subscriptions.Commands.CancelSubscription;

internal sealed class CancelSubscriptionCommandHandler : IRequestHandler<CancelSubscriptionCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CancelSubscriptionCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(CancelSubscriptionCommand request, CancellationToken ct)
    {
        var subRepo = _uow.Repository<OwnerSubscription, int>();
        var ownSubs = await subRepo.FindAsync(s => s.OwnerId == _currentUser.UserId, ct);
        var lastSub = ownSubs.OrderByDescending(s => s.StartedAt).FirstOrDefault()
            ?? throw new DomainException("Bạn chưa từng đăng ký gói subscription nào.");

        var now = DateTimeOffset.UtcNow;
        var isActive = lastSub.Status == SubscriptionStatus.Active && lastSub.ExpiresAt > now;
        if (!isActive)
            throw new ConflictException("Bạn không có gói subscription nào đang hoạt động để hủy.");

        lastSub.Status = SubscriptionStatus.Cancelled;
        lastSub.CancelledAt = now;
        subRepo.Update(lastSub);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
