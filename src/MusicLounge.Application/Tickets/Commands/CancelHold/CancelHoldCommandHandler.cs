using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.CancelHold;

internal sealed class CancelHoldCommandHandler : IRequestHandler<CancelHoldCommand, bool>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAsyncKeyedLock _lock;

    public CancelHoldCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _lock = @lock;
    }

    public async Task<bool> Handle(CancelHoldCommand request, CancellationToken ct)
    {
        // Same key PurchaseTicketCommandHandler locks on — without this, a cancel racing a
        // concurrent purchase of the same hold could delete the TicketHold row out from under an
        // in-flight purchase (or vice versa), instead of one of the two cleanly failing first.
        await using var _ = await _lock.AcquireAsync($"purchase-hold:{request.HoldId}", ct);

        var holdRepo = _uow.Repository<TicketHold, int>();
        var hold = await holdRepo.GetByIdAsync(request.HoldId, ct)
            ?? throw new NotFoundException(nameof(TicketHold), request.HoldId);

        if (hold.UserId != _currentUser.UserId)
            throw new ForbiddenException("Vé giữ chỗ này không thuộc về bạn.");

        // A hold already spent by PurchaseTicketCommandHandler (Payment + Pending tickets already
        // created) must not be silently deleted — it was never checked before, so a stale client
        // retry after a successful purchase could remove the hold record backing a real payment.
        if (hold.IsReleased)
            throw new ConflictException("Vé giữ chỗ này đã được dùng để mua vé, không thể huỷ.");

        holdRepo.Remove(hold);
        await _uow.SaveChangesAsync(ct);

        return true;
    }
}
