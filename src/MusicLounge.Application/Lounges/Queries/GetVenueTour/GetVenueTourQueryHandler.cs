using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Queries.GetVenueTour;

// Public/anonymous — an Audience previews the venue before buying a ticket, same spirit as
// GetPublicHistoryByPerformerAsync.
internal sealed class GetVenueTourQueryHandler : IRequestHandler<GetVenueTourQuery, VenueTourDto>
{
    private readonly IUnitOfWork _uow;

    public GetVenueTourQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<VenueTourDto> Handle(GetVenueTourQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        var scenes = await _uow.Repository<VenueTourScene, int>().FindAsync(s => s.LoungeId == request.LoungeId, ct);
        var sceneIds = scenes.Select(s => s.Id).ToHashSet();

        // Generic IRepository never eager-loads navigation properties — fetch all hotspots for
        // this lounge's scenes in one round trip, then group in memory instead of N+1-ing per scene.
        var allHotspots = await _uow.Repository<VenueTourHotspot, int>()
            .FindAsync(h => sceneIds.Contains(h.SceneId), ct);
        var hotspotsBySceneId = allHotspots.GroupBy(h => h.SceneId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var sceneDtos = scenes
            .OrderBy(s => s.OrderIndex)
            .Select(s => new VenueTourSceneDto(
                s.Id,
                s.ImageUrl,
                s.Name,
                s.OrderIndex,
                s.PositionX,
                s.PositionY,
                (hotspotsBySceneId.TryGetValue(s.Id, out var hotspots) ? hotspots : [])
                    .Select(h => new VenueTourHotspotDto(
                        h.Id, h.Type.ToString(), h.Yaw, h.Pitch, h.Label, h.TargetSceneId, h.InfoText))
                    .ToList()))
            .ToList();

        return new VenueTourDto(request.LoungeId, lounge.AreaLayoutImageUrl, sceneDtos);
    }
}
