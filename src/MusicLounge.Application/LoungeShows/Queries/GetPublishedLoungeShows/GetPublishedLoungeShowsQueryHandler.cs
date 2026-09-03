using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Queries.GetPublishedLoungeShows;

internal sealed class GetPublishedLoungeShowsQueryHandler
    : IRequestHandler<GetPublishedLoungeShowsQuery, PaginatedResult<LoungeShowListItemDto>>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;

    public GetPublishedLoungeShowsQueryHandler(
        ILoungeShowRepository showRepo,
        ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<LoungeShowListItemDto>> Handle(
        GetPublishedLoungeShowsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        PaginatedResult<Domain.Entities.LoungeShow> result;
        if (request.Mine)
        {
            if (!_currentUser.IsAuthenticated)
                throw new UnauthorizedException("Vui lòng đăng nhập để xem event của bạn.");

            result = await _showRepo.GetMineAsync(
                _currentUser.UserId, page, pageSize, request.SortBy, status: null, ct);
        }
        else
        {
            result = await _showRepo.GetPublishedAsync(
                page, pageSize, request.SortBy, request.IncludeSoldOut, ct);
        }

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
