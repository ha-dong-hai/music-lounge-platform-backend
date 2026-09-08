using MediatR;
using MusicLounge.Application.Admin.DTOs;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Admin.Queries.GetSystemConfigs;

internal sealed class GetSystemConfigsQueryHandler
    : IRequestHandler<GetSystemConfigsQuery, IReadOnlyList<SystemConfigDto>>
{
    private readonly IUnitOfWork _uow;

    public GetSystemConfigsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<SystemConfigDto>> Handle(
        GetSystemConfigsQuery request, CancellationToken ct)
    {
        var configs = await _uow.Repository<SystemConfig, int>().FindAsync(c => c.Id > 0, ct);

        var editorIds = configs.Where(c => c.UpdatedBy.HasValue).Select(c => c.UpdatedBy!.Value).Distinct().ToList();
        var editors = editorIds.Count > 0
            ? (await _uow.Repository<User, int>().FindAsync(u => editorIds.Contains(u.Id), ct))
                .ToDictionary(u => u.Id, u => u.FullName)
            : [];

        return configs
            .OrderBy(c => c.ConfigKey, StringComparer.Ordinal)
            .Select(c => new SystemConfigDto(
                c.ConfigKey,
                c.ConfigValue,
                c.DataType,
                c.Description,
                SystemConfigValidation.IsMoneyRate(c.ConfigKey),
                c.UpdatedAt,
                c.UpdatedBy,
                c.UpdatedBy.HasValue ? editors.GetValueOrDefault(c.UpdatedBy.Value) : null))
            .ToList();
    }
}
