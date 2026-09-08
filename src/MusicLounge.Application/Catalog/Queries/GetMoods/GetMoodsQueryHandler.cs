using MediatR;
using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Catalog.Queries.GetMoods;

internal sealed class GetMoodsQueryHandler : IRequestHandler<GetMoodsQuery, List<CatalogItemDto>>
{
    private readonly IRepository<Mood, int> _repo;

    public GetMoodsQueryHandler(IRepository<Mood, int> repo) => _repo = repo;

    public async Task<List<CatalogItemDto>> Handle(GetMoodsQuery request, CancellationToken ct)
    {
        var moods = await _repo.GetAllAsync(ct);
        return moods.Select(m => new CatalogItemDto(m.Id, m.Name)).ToList();
    }
}
