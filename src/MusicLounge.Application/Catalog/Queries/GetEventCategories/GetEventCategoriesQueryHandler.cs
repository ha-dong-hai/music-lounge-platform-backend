using MediatR;
using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Catalog.Queries.GetEventCategories;

internal sealed class GetEventCategoriesQueryHandler
    : IRequestHandler<GetEventCategoriesQuery, List<CatalogItemDto>>
{
    private readonly IRepository<EventCategory, int> _repo;

    public GetEventCategoriesQueryHandler(IRepository<EventCategory, int> repo) => _repo = repo;

    public async Task<List<CatalogItemDto>> Handle(GetEventCategoriesQuery request, CancellationToken ct)
    {
        // Chi tra danh muc dang active - IsActive ton tai chinh de an mem danh muc cu ma khong xoa.
        var categories = await _repo.FindAsync(c => c.IsActive, ct);
        return categories.Select(c => new CatalogItemDto(c.Id, c.Name)).ToList();
    }
}
