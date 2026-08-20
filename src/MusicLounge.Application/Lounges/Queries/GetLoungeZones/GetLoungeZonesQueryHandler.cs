using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Lounges.Queries.GetLoungeZones;

internal sealed class GetLoungeZonesQueryHandler
    : IRequestHandler<GetLoungeZonesQuery, IReadOnlyList<SeatingZoneDto>>
{
    private readonly IUnitOfWork _uow;

    public GetLoungeZonesQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<SeatingZoneDto>> Handle(GetLoungeZonesQuery request, CancellationToken ct)
    {
        var zones = await _uow.Repository<SeatingZone, int>().FindAsync(
            z => z.LoungeId == request.LoungeId && (!request.ActiveOnly || z.IsActive), ct);

        return zones
            .OrderBy(z => z.DisplayOrder)
            .Select(z => new SeatingZoneDto(
                z.Id, z.LoungeId, z.Name, z.Description, z.Capacity, z.DisplayOrder, z.IsActive,
                z.LayoutColor, z.Layout2DX, z.Layout2DY, z.Layout2DWidth, z.Layout2DHeight, z.Layout2DRotationDeg,
                z.Layout3DX, z.Layout3DY, z.Layout3DZ))
            .ToList();
    }
}
