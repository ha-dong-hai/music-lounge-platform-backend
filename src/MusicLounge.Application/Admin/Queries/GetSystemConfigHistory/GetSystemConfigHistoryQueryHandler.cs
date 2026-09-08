using MediatR;
using MusicLounge.Application.Admin.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Admin.Queries.GetSystemConfigHistory;

internal sealed class GetSystemConfigHistoryQueryHandler
    : IRequestHandler<GetSystemConfigHistoryQuery, IReadOnlyList<SystemConfigHistoryDto>>
{
    private readonly IUnitOfWork _uow;

    public GetSystemConfigHistoryQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<SystemConfigHistoryDto>> Handle(
        GetSystemConfigHistoryQuery request, CancellationToken ct)
    {
        var rows = await _uow.Repository<SystemConfigHistory, long>()
            .FindAsync(h => h.ConfigKey == request.ConfigKey, ct);

        var actorIds = rows.Select(h => h.ChangedBy).Distinct().ToList();
        var actors = actorIds.Count > 0
            ? (await _uow.Repository<User, int>().FindAsync(u => actorIds.Contains(u.Id), ct))
                .ToDictionary(u => u.Id, u => u.FullName)
            : [];

        // Mới nhất lên đầu — câu hỏi thường gặp nhất là "lần cuối đổi là khi nào, ai đổi".
        return rows
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new SystemConfigHistoryDto(
                h.Id, h.ConfigKey, h.OldValue, h.NewValue, h.Note,
                h.ChangedAt, h.ChangedBy, actors.GetValueOrDefault(h.ChangedBy)))
            .ToList();
    }
}
