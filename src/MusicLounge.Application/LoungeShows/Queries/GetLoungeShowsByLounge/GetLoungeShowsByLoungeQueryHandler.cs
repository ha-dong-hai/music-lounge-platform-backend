using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowsByLounge;

internal sealed class GetLoungeShowsByLoungeQueryHandler
    : IRequestHandler<GetLoungeShowsByLoungeQuery, PaginatedResult<LoungeShowListItemDto>>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;

    public GetLoungeShowsByLoungeQueryHandler(
        ILoungeShowRepository showRepo,
        ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<LoungeShowListItemDto>> Handle(
        GetLoungeShowsByLoungeQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await _showRepo.GetByLoungeAsync(
            request.LoungeId, page, pageSize, ct);

        var wishlisted = _currentUser.IsAuthenticated
            ? await _showRepo.GetWishlistedShowIdsAsync(_currentUser.UserId, ct)
            : (IReadOnlySet<int>)new HashSet<int>();

        var items = result.Items.Select(s => s.ToListItemDto(wishlisted)).ToList();

        return new PaginatedResult<LoungeShowListItemDto>(
            items, result.Page, result.PageSize, result.TotalCount);
    }
}
