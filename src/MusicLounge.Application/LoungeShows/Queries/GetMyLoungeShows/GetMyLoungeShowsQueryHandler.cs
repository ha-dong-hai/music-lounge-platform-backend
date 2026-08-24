using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetMyLoungeShows;

// MLACP-44: danh sach su kien CUA CHINH Owner dang dang nhap (moi trang thai, ke ca Draft), khac
// GetLoungeShowsByLoungeQuery (cong khai, theo 1 loungeId bat ky, khong loc quyen so huu).
internal sealed class GetMyLoungeShowsQueryHandler
    : IRequestHandler<GetMyLoungeShowsQuery, PaginatedResult<LoungeShowListItemDto>>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;

    public GetMyLoungeShowsQueryHandler(ILoungeShowRepository showRepo, ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<LoungeShowListItemDto>> Handle(
        GetMyLoungeShowsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await _showRepo.GetMineAsync(
            _currentUser.UserId, page, pageSize, request.SortBy, request.Status, ct);

        var items = result.Items.Select(s => s.ToListItemDto()).ToList();
        return new PaginatedResult<LoungeShowListItemDto>(items, result.Page, result.PageSize, result.TotalCount);
    }
}
