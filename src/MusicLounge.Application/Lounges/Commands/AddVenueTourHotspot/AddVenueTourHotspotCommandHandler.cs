using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.AddVenueTourHotspot;

internal sealed class AddVenueTourHotspotCommandHandler : IRequestHandler<AddVenueTourHotspotCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public AddVenueTourHotspotCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(AddVenueTourHotspotCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var sceneRepo = _uow.Repository<VenueTourScene, int>();
        var scene = await sceneRepo.GetByIdAsync(request.SceneId, ct);
        if (scene is null || scene.LoungeId != request.LoungeId)
            throw new NotFoundException(nameof(VenueTourScene), request.SceneId);

        if (request.TargetSceneId.HasValue)
        {
            var targetScene = await sceneRepo.GetByIdAsync(request.TargetSceneId.Value, ct);
            // Can't navigate to another venue's scene — the two tours are unrelated walkthroughs.
            if (targetScene is null || targetScene.LoungeId != request.LoungeId)
                throw new NotFoundException(nameof(VenueTourScene), request.TargetSceneId.Value);
        }

        var type = Enum.Parse<VenueTourHotspotType>(request.Type, ignoreCase: true);
        var hotspot = new VenueTourHotspot
        {
            SceneId = request.SceneId,
            TargetSceneId = type == VenueTourHotspotType.Navigate ? request.TargetSceneId : null,
            Type = type,
            Yaw = request.Yaw,
            Pitch = request.Pitch,
            Label = request.Label,
            InfoText = request.InfoText
        };
        _uow.Repository<VenueTourHotspot, int>().Add(hotspot);
        await _uow.SaveChangesAsync(ct);
        return hotspot.Id;
    }
}
