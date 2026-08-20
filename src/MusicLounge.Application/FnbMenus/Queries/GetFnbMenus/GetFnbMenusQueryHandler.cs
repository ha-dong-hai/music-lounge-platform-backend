using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.FnbMenus.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.FnbMenus.Queries.GetFnbMenus;

internal sealed class GetFnbMenusQueryHandler
    : IRequestHandler<GetFnbMenusQuery, IReadOnlyList<FnbMenuDto>>
{
    private readonly IUnitOfWork _uow;

    public GetFnbMenusQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<FnbMenuDto>> Handle(GetFnbMenusQuery request, CancellationToken ct)
    {
        var menus = await _uow.Repository<FnbMenu, int>().FindAsync(
            m => m.LoungeId == request.LoungeId && (!request.ActiveOnly || m.IsActive), ct);

        return menus
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new FnbMenuDto(
                m.Id, m.LoungeId, m.Name, m.Description, m.IsActive, m.DisplayOrder))
            .ToList();
    }
}
