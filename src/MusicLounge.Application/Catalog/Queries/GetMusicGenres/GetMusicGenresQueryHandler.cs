using MediatR;
using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Catalog.Queries.GetMusicGenres;

internal sealed class GetMusicGenresQueryHandler
    : IRequestHandler<GetMusicGenresQuery, List<MusicGenreCatalogItemDto>>
{
    private readonly IRepository<MusicGenre, Guid> _repo;

    public GetMusicGenresQueryHandler(IRepository<MusicGenre, Guid> repo) => _repo = repo;

    public async Task<List<MusicGenreCatalogItemDto>> Handle(GetMusicGenresQuery request, CancellationToken ct)
    {
        var genres = await _repo.GetAllAsync(ct);
        return genres.Select(g => new MusicGenreCatalogItemDto(g.Id, g.Name, g.ImageUrl)).ToList();
    }
}
