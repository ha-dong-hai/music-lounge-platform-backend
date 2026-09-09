using MediatR;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.DTOs;

namespace MusicLounge.Application.Admin.Queries.GetVenueReviewQueue;

internal sealed class GetVenueReviewQueueQueryHandler
    : IRequestHandler<GetVenueReviewQueueQuery, PaginatedResult<VenueReviewItemDto>>
{
    private readonly ILoungeRepository _repo;

    public GetVenueReviewQueueQueryHandler(ILoungeRepository repo) => _repo = repo;

    public async Task<PaginatedResult<VenueReviewItemDto>> Handle(
        GetVenueReviewQueueQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        return await _repo.GetReviewQueueAsync(request.Status, page, size, ct);
    }
}
