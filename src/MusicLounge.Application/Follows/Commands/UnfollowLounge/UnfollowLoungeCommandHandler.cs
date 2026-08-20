using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Follows.Commands.UnfollowLounge;

internal sealed class UnfollowLoungeCommandHandler : IRequestHandler<UnfollowLoungeCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UnfollowLoungeCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UnfollowLoungeCommand request, CancellationToken ct)
    {
        var matches = await _uow.Repository<Follow, int>()
            .FindAsync(f => f.UserId == _currentUser.UserId && f.LoungeId == request.LoungeId, ct);

        var follow = matches.FirstOrDefault()
            ?? throw new NotFoundException("Follow", request.LoungeId);

        _uow.Repository<Follow, int>().Remove(follow);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
