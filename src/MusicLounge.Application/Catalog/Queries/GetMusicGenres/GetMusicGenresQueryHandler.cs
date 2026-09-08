using MediatR;
using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Catalog.Queries.GetMusicGenres;

internal sealed class GetMusicGenresQueryHandler
    : IRequestHandler<GetMusicGenresQuery, List<CatalogItemDto>>
{
    private readonly IRepository<MusicGenre, int> _repo;

    public GetMusicGenresQueryHandler(IRepository<MusicGenre, int> repo) => _repo = repo;

    public async Task<List<CatalogItemDto>> Handle(GetMusicGenresQuery request, CancellationToken ct)
    {
        var genres = await _repo.GetAllAsync(ct);
        return genres.Select(g => new CatalogItemDto(g.Id, g.Name)).ToList();
    }
}
