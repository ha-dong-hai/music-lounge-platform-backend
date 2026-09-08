using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Queries.GetMoods;

public sealed record GetMoodsQuery : IQuery<List<CatalogItemDto>>;
