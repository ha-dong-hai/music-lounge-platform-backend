using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Queries.GetEventCategories;

public sealed record GetEventCategoriesQuery : IQuery<List<CatalogItemDto>>;
