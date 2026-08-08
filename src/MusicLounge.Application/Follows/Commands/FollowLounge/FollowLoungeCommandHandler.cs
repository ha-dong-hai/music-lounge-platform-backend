using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Follows.Commands.FollowLounge;

internal sealed class FollowLoungeCommandHandler : IRequestHandler<FollowLoungeCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public FollowLoungeCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(FollowLoungeCommand request, CancellationToken ct)
    {
        var loungeExists = await _uow.Repository<MusicLounge.Domain.Entities.MusicLounge, int>()
            .AnyAsync(l => l.Id == request.LoungeId, ct);

        if (!loungeExists)
            throw new NotFoundException("Lounge", request.LoungeId);

        var alreadyFollowing = await _uow.Repository<Follow, int>()
            .AnyAsync(f => f.UserId == _currentUser.UserId && f.LoungeId == request.LoungeId, ct);

        if (alreadyFollowing)
            throw new ConflictException("Bạn đã theo dõi phòng trà này.");

        _uow.Repository<Follow, int>().Add(new Follow
        {
            UserId = _currentUser.UserId,
            LoungeId = request.LoungeId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
