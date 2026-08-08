using MediatR;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Moderations.Queries.GetPendingModerations;

internal sealed class GetPendingModerationsQueryHandler
    : IRequestHandler<GetPendingModerationsQuery, PaginatedResult<EventModerationDto>>
{
    private readonly IEventModerationRepository _repo;

    public GetPendingModerationsQueryHandler(IEventModerationRepository repo) => _repo = repo;

    public async Task<PaginatedResult<EventModerationDto>> Handle(
        GetPendingModerationsQuery request,
        CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);

        ModerationTargetType? targetType = null;
        if (!string.IsNullOrEmpty(request.TargetType) &&
            Enum.TryParse<ModerationTargetType>(request.TargetType, true, out var parsed))
            targetType = parsed;

        return await _repo.GetPendingAsync(targetType, page, size, ct);
    }
}
