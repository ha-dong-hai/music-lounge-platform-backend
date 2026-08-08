using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.CancelHold;

internal sealed class CancelHoldCommandHandler : IRequestHandler<CancelHoldCommand, bool>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CancelHoldCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(CancelHoldCommand request, CancellationToken ct)
    {
        var holdRepo = _uow.Repository<TicketHold, int>();
        var hold = await holdRepo.GetByIdAsync(request.HoldId, ct)
            ?? throw new NotFoundException(nameof(TicketHold), request.HoldId);

        if (hold.UserId != _currentUser.UserId)
            throw new ForbiddenException("Vé giữ chỗ này không thuộc về bạn.");

        holdRepo.Remove(hold);
        await _uow.SaveChangesAsync(ct);

        return true;
    }
}
