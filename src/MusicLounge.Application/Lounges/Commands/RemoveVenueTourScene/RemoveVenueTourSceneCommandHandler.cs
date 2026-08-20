using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.RemoveVenueTourScene;

internal sealed class RemoveVenueTourSceneCommandHandler : IRequestHandler<RemoveVenueTourSceneCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public RemoveVenueTourSceneCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RemoveVenueTourSceneCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var sceneRepo = _uow.Repository<VenueTourScene, int>();
        var scene = await sceneRepo.GetByIdAsync(request.SceneId, ct);
        if (scene is null || scene.LoungeId != request.LoungeId)
            throw new NotFoundException(nameof(VenueTourScene), request.SceneId);

        // VenueTourHotspot.TargetSceneId is Restrict (not Cascade — two cascade paths into the
        // same table from the same row isn't allowed by SQL Server), so any hotspot elsewhere in
        // this tour that navigates TO this scene must be cleaned up explicitly first, or the
        // delete below would fail with an FK violation. Hotspots physically INSIDE this scene
        // don't need handling here — those cascade via the Scene FK automatically.
        var hotspotRepo = _uow.Repository<VenueTourHotspot, int>();
        var incomingHotspots = await hotspotRepo.FindAsync(h => h.TargetSceneId == request.SceneId, ct);
        foreach (var hotspot in incomingHotspots)
            hotspotRepo.Remove(hotspot);

        // Same reasoning as the hotspot cleanup above — VenueTourStitchAttempt.ResultSceneId is
        // NoAction (not Cascade/SetNull, to avoid a second cascade path from MusicLounge into that
        // table), so any attempt log pointing at this scene needs its reference cleared explicitly
        // before the scene is deleted. The log ROW itself stays (it's an audit trail), just its
        // link to a now-gone scene is nulled.
        var attemptRepo = _uow.Repository<VenueTourStitchAttempt, int>();
        var referencingAttempts = await attemptRepo.FindAsync(a => a.ResultSceneId == request.SceneId, ct);
        foreach (var attempt in referencingAttempts)
        {
            attempt.ResultSceneId = null;
            attemptRepo.Update(attempt);
        }

        sceneRepo.Remove(scene);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
