using MediatR;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.DTOs;

namespace MusicLounge.Application.Moderations.Queries.GetPendingLoungeShows;

internal sealed class GetPendingLoungeShowsQueryHandler
    : IRequestHandler<GetPendingLoungeShowsQuery, PaginatedResult<PendingLoungeShowDto>>
{
    private readonly IEventModerationRepository _repo;

    public GetPendingLoungeShowsQueryHandler(IEventModerationRepository repo) => _repo = repo;

    public async Task<PaginatedResult<PendingLoungeShowDto>> Handle(
        GetPendingLoungeShowsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        return await _repo.GetPendingShowsAsync(page, size, ct);
    }
}
