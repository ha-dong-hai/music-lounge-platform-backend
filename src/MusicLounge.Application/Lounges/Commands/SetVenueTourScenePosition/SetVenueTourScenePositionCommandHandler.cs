using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.SetVenueTourScenePosition;

internal sealed class SetVenueTourScenePositionCommandHandler
    : IRequestHandler<SetVenueTourScenePositionCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetVenueTourScenePositionCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetVenueTourScenePositionCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var sceneRepo = _uow.Repository<VenueTourScene, int>();
        var scene = await sceneRepo.GetByIdAsync(request.SceneId, ct);
        if (scene is null || scene.LoungeId != request.LoungeId)
            throw new NotFoundException(nameof(VenueTourScene), request.SceneId);

        scene.PositionX = request.X;
        scene.PositionY = request.Y;
        sceneRepo.Update(scene);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
