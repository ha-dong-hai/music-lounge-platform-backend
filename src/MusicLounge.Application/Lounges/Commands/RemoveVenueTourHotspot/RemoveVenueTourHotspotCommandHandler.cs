using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.RemoveVenueTourHotspot;

internal sealed class RemoveVenueTourHotspotCommandHandler : IRequestHandler<RemoveVenueTourHotspotCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public RemoveVenueTourHotspotCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RemoveVenueTourHotspotCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var hotspotRepo = _uow.Repository<VenueTourHotspot, int>();
        var hotspot = await hotspotRepo.GetByIdAsync(request.HotspotId, ct)
            ?? throw new NotFoundException(nameof(VenueTourHotspot), request.HotspotId);

        // Generic IRepository never eager-loads navigation properties — look the scene up
        // directly rather than trusting hotspot.Scene to be populated.
        var scene = await _uow.Repository<VenueTourScene, int>().GetByIdAsync(hotspot.SceneId, ct);
        if (scene is null || scene.LoungeId != request.LoungeId)
            throw new NotFoundException(nameof(VenueTourHotspot), request.HotspotId);

        hotspotRepo.Remove(hotspot);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
