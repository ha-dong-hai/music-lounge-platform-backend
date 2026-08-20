using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.FnbMenuItems.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.FnbMenuItems.Queries.GetMenuItems;

internal sealed class GetMenuItemsQueryHandler
    : IRequestHandler<GetMenuItemsQuery, IReadOnlyList<FnbMenuItemDto>>
{
    private readonly IUnitOfWork _uow;

    public GetMenuItemsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<FnbMenuItemDto>> Handle(
        GetMenuItemsQuery request, CancellationToken ct)
    {
        var items = await _uow.Repository<FnbMenuItem, int>().FindAsync(
            m => m.MenuId == request.MenuId && (!request.AvailableOnly || m.IsAvailable), ct);

        return items
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new FnbMenuItemDto(
                m.Id, m.MenuId, m.Category, m.Name, m.Description,
                m.Price, m.ImageUrl, m.IsAvailable, m.DisplayOrder))
            .ToList();
    }
}
