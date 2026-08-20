using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetUserWishlist;

internal sealed class GetUserWishlistQueryHandler
    : IRequestHandler<GetUserWishlistQuery, PaginatedResult<LoungeShowListItemDto>>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;

    public GetUserWishlistQueryHandler(
        ILoungeShowRepository showRepo,
        ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<LoungeShowListItemDto>> Handle(
        GetUserWishlistQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await _showRepo.GetWishlistByUserAsync(
            _currentUser.UserId, page, pageSize, ct);

        var items = result.Items
            .Select(s => s.ToListItemDto(isWishlisted: true))
            .ToList();

        return new PaginatedResult<LoungeShowListItemDto>(
            items, result.Page, result.PageSize, result.TotalCount);
    }
}
