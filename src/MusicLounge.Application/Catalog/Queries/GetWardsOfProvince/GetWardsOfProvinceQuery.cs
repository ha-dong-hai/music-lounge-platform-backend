using MediatR;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Queries.GetWardsOfProvince;

/// <summary>
/// MLACP-521: phường/xã của một tỉnh. Không còn cấp quận/huyện ở giữa (Nghị quyết 202/2025/QH15) — đây là thứ thay cho
/// "danh sách quận" mà giao diện từng gọi (/lounge-shows/filter-options/districts, chưa từng tồn tại).
/// </summary>
public sealed record GetWardsOfProvinceQuery(string ProvinceCode) : IQuery<IReadOnlyList<AdministrativeWard>>;

internal sealed class GetWardsOfProvinceQueryHandler(IAdministrativeUnitCatalog catalog)
    : IRequestHandler<GetWardsOfProvinceQuery, IReadOnlyList<AdministrativeWard>>
{
    public Task<IReadOnlyList<AdministrativeWard>> Handle(GetWardsOfProvinceQuery request, CancellationToken ct)
    {
        var province = catalog.FindProvince(request.ProvinceCode)
            ?? throw new NotFoundException(nameof(AdministrativeProvince), request.ProvinceCode);
        return Task.FromResult(catalog.WardsOf(province.Code));
    }
}
