using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Queries.GetVenueAtmospheres;

public sealed record GetVenueAtmospheresQuery : IQuery<List<CatalogItemDto>>;
