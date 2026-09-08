using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Queries.GetMusicGenres;

public sealed record GetMusicGenresQuery : IQuery<List<CatalogItemDto>>;
