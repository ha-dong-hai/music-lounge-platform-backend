using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.SearchLoungeShows;

internal sealed class SearchLoungeShowsQueryHandler
    : IRequestHandler<SearchLoungeShowsQuery, PaginatedResult<LoungeShowListItemDto>>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;

    public SearchLoungeShowsQueryHandler(
        ILoungeShowRepository showRepo,
        ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<LoungeShowListItemDto>> Handle(
        SearchLoungeShowsQuery request, CancellationToken ct)
    {
        request = request with
        {
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };
        var searchParams = request.ToSearchParams();
        var result = await _showRepo.SearchAsync(searchParams, ct);

        var wishlisted = _currentUser.IsAuthenticated
            ? await _showRepo.GetWishlistedShowIdsAsync(_currentUser.UserId, ct)
            : (IReadOnlySet<int>)new HashSet<int>();

        var items = result.Items
            .Select(s => s.ToListItemDto(wishlisted))
            .ToList();

        return new PaginatedResult<LoungeShowListItemDto>(
            items, result.Page, result.PageSize, result.TotalCount);
    }
}
