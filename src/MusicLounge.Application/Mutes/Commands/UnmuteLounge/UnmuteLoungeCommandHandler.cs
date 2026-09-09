using MediatR;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Mutes.Commands.UnmuteLounge;

internal sealed class UnmuteLoungeCommandHandler : ICommandHandler<UnmuteLoungeCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UnmuteLoungeCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UnmuteLoungeCommand request, CancellationToken ct)
    {
        var matches = await _uow.Repository<LoungeMute, int>()
            .FindAsync(m => m.UserId == _currentUser.UserId && m.LoungeId == request.LoungeId, ct);

        var mute = matches.FirstOrDefault()
            ?? throw new NotFoundException("LoungeMute", request.LoungeId);

        _uow.Repository<LoungeMute, int>().Remove(mute);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
