using MediatR;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Catalog.Queries.GetProvinces;

/// <summary>MLACP-521: 34 tỉnh/thành phố từ 01/7/2025 (QĐ 19/2025/QĐ-TTg), theo thứ tự mã chính thức.</summary>
public sealed record GetProvincesQuery : IQuery<IReadOnlyList<AdministrativeProvince>>;

internal sealed class GetProvincesQueryHandler(IAdministrativeUnitCatalog catalog)
    : IRequestHandler<GetProvincesQuery, IReadOnlyList<AdministrativeProvince>>
{
    public Task<IReadOnlyList<AdministrativeProvince>> Handle(GetProvincesQuery request, CancellationToken ct)
        => Task.FromResult(catalog.Provinces);
}
