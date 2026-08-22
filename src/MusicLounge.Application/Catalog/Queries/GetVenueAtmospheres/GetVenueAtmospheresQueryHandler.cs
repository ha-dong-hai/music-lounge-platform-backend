using MediatR;
using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Catalog.Queries.GetVenueAtmospheres;

internal sealed class GetVenueAtmospheresQueryHandler
    : IRequestHandler<GetVenueAtmospheresQuery, List<CatalogItemDto>>
{
    private readonly IRepository<VenueAtmosphere, int> _repo;

    public GetVenueAtmospheresQueryHandler(IRepository<VenueAtmosphere, int> repo) => _repo = repo;

    public async Task<List<CatalogItemDto>> Handle(GetVenueAtmospheresQuery request, CancellationToken ct)
    {
        var atmospheres = await _repo.GetAllAsync(ct);
        return atmospheres.Select(a => new CatalogItemDto(a.Id, a.Name)).ToList();
    }
}
